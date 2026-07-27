using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Google.Apis.Auth;
using Microsoft.IdentityModel.Tokens;

namespace InvoicesBackend.Services;

/// <summary>
/// The result of verifying a token from an external identity provider
/// (Google / Facebook / Apple) — the minimal profile info we need to
/// find-or-create a local user account.
/// </summary>
public class ExternalUserInfo
{
    public string Provider { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
}

/// <summary>
/// Verifies sign-in tokens issued by Google, Facebook, and Apple.
///
/// Configuration (appsettings.json → "Authentication"):
///   "Google":   { "ClientId": "&lt;your Google OAuth client id&gt;.apps.googleusercontent.com" }
///   "Facebook": { "AppId": "&lt;your Facebook App ID&gt;", "AppSecret": "&lt;your Facebook App Secret&gt;" }
///   "Apple":    { "ClientId": "&lt;your Service ID / bundle id used as the 'aud' claim&gt;" }
/// </summary>
public class ExternalAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    private static JsonWebKeySet? _appleKeysCache;
    private static DateTime _appleKeysCacheExpiry = DateTime.MinValue;

    public ExternalAuthService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ExternalUserInfo> VerifyAsync(string provider, string token)
    {
        return provider.ToLowerInvariant() switch
        {
            "google" => await VerifyGoogleAsync(token),
            "facebook" => await VerifyFacebookAsync(token),
            "apple" => await VerifyAppleAsync(token),
            _ => throw new ArgumentException($"Unsupported provider '{provider}'")
        };
    }

    private async Task<ExternalUserInfo> VerifyGoogleAsync(string idToken)
    {
        var clientId = _configuration["Authentication:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Google sign-in is not configured (Authentication:Google:ClientId is missing).");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            });
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException("Invalid Google token", ex);
        }

        if (string.IsNullOrWhiteSpace(payload.Email))
            throw new UnauthorizedAccessException("Google account has no email address");

        return new ExternalUserInfo
        {
            Provider = "google",
            ExternalId = payload.Subject,
            Email = payload.Email,
            FullName = payload.Name
        };
    }

    private async Task<ExternalUserInfo> VerifyFacebookAsync(string accessToken)
    {
        var appId = _configuration["Authentication:Facebook:AppId"];
        var appSecret = _configuration["Authentication:Facebook:AppSecret"];

        var client = _httpClientFactory.CreateClient();

        // Verify the token actually belongs to our app before trusting it.
        if (!string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(appSecret))
        {
            var debugUrl = $"https://graph.facebook.com/debug_token?input_token={Uri.EscapeDataString(accessToken)}&access_token={Uri.EscapeDataString(appId)}|{Uri.EscapeDataString(appSecret)}";
            var debugResponse = await client.GetAsync(debugUrl);
            if (!debugResponse.IsSuccessStatusCode)
                throw new UnauthorizedAccessException("Could not verify Facebook token");

            using var debugDoc = JsonDocument.Parse(await debugResponse.Content.ReadAsStringAsync());
            var data = debugDoc.RootElement.GetProperty("data");

            var isValid = data.TryGetProperty("is_valid", out var validProp) && validProp.GetBoolean();
            var tokenAppId = data.TryGetProperty("app_id", out var appIdProp) ? appIdProp.GetString() : null;

            if (!isValid || tokenAppId != appId)
                throw new UnauthorizedAccessException("Invalid Facebook token");
        }

        var meResponse = await client.GetAsync($"https://graph.facebook.com/me?fields=id,name,email&access_token={Uri.EscapeDataString(accessToken)}");
        if (!meResponse.IsSuccessStatusCode)
            throw new UnauthorizedAccessException("Could not fetch Facebook profile");

        using var meDoc = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync());
        var root = meDoc.RootElement;

        var id = root.GetProperty("id").GetString() ?? throw new UnauthorizedAccessException("Facebook profile missing id");
        var email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
        var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;

        if (string.IsNullOrWhiteSpace(email))
            throw new UnauthorizedAccessException("This Facebook account has no email address on file. Please use a different sign-in method.");

        return new ExternalUserInfo
        {
            Provider = "facebook",
            ExternalId = id,
            Email = email,
            FullName = name
        };
    }

    private async Task<ExternalUserInfo> VerifyAppleAsync(string identityToken)
    {
        var clientId = _configuration["Authentication:Apple:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Apple sign-in is not configured (Authentication:Apple:ClientId is missing).");

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(identityToken))
            throw new UnauthorizedAccessException("Invalid Apple token");

        var jwt = handler.ReadJwtToken(identityToken);
        var kid = jwt.Header.Kid;

        var keys = await GetAppleSigningKeysAsync();
        var key = keys.Keys.FirstOrDefault(k => k.Kid == kid)
            ?? throw new UnauthorizedAccessException("Could not find a matching Apple signing key");

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://appleid.apple.com",
            ValidateAudience = true,
            ValidAudience = clientId,
            ValidateLifetime = true,
            IssuerSigningKey = key
        };

        try
        {
            handler.ValidateToken(identityToken, validationParameters, out _);
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException("Invalid Apple token", ex);
        }

        var subject = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
            ?? throw new UnauthorizedAccessException("Apple token missing subject");
        var email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

        if (string.IsNullOrWhiteSpace(email))
            throw new UnauthorizedAccessException("This Apple account did not share an email address. Please use a different sign-in method.");

        return new ExternalUserInfo
        {
            Provider = "apple",
            ExternalId = subject,
            Email = email,
            FullName = null // Apple only sends name separately on first sign-in (handled by the frontend/DTO.FullName)
        };
    }

    private async Task<JsonWebKeySet> GetAppleSigningKeysAsync()
    {
        if (_appleKeysCache != null && _appleKeysCacheExpiry > DateTime.UtcNow)
            return _appleKeysCache;

        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync("https://appleid.apple.com/auth/keys");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var keySet = new JsonWebKeySet(json);

        _appleKeysCache = keySet;
        _appleKeysCacheExpiry = DateTime.UtcNow.AddHours(6);

        return keySet;
    }
}
