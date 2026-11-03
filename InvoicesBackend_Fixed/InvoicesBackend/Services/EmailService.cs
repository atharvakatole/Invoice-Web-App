using System.Net;
using System.Net.Mail;
using System.Net.Http.Json;
using System.Text.Json;

namespace InvoicesBackend.Services;

/// <summary>
/// Sends transactional emails through Brevo's HTTP API or SMTP.
///   "Email": {
///     "Provider": "Brevo",
///     "ApiKey": "...",
///     "FromAddress": "verified-sender@example.com",
///     "FrontendBaseUrl": "https://your-app.example.com",
///     "SmtpHost": "smtp.gmail.com",
///     "SmtpPort": 587,
///     "Username": "your@gmail.com",
///     "Password": "app-password",
///     "FromAddress": "your@gmail.com",
///     "FromName": "Invoicely"
///   }
/// In Development, emails are logged to console instead of sent.
/// </summary>
public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory? _httpClientFactory;

    public EmailService(
        IConfiguration config,
        ILogger<EmailService> logger,
        IWebHostEnvironment env,
        IHttpClientFactory? httpClientFactory = null)
    {
        _config = config;
        _logger = logger;
        _env = env;
        _httpClientFactory = httpClientFactory;
    }

    public async Task SendOtpAsync(string toEmail, string otpCode, string purpose)
    {
        var subject = purpose == "registration"
            ? "Your Invoicely verification code"
            : "Reset your Invoicely password";

        var body = purpose == "registration"
            ? $@"<div style='font-family:Inter,sans-serif;max-width:480px;margin:0 auto;padding:32px;'>
                   <h2 style='margin-bottom:8px;'>Verify your email</h2>
                   <p style='color:#666;margin-bottom:24px;'>Enter this code to complete your Invoicely registration. It expires in <strong>10 minutes</strong>.</p>
                   <div style='background:#f4f6fb;border-radius:12px;padding:24px;text-align:center;margin-bottom:24px;'>
                     <span style='font-size:40px;font-weight:700;letter-spacing:12px;font-family:monospace;'>{otpCode}</span>
                   </div>
                   <p style='color:#999;font-size:13px;'>If you didn't create an account, ignore this email.</p>
                 </div>"
            : $@"<div style='font-family:Inter,sans-serif;max-width:480px;margin:0 auto;padding:32px;'>
                   <h2 style='margin-bottom:8px;'>Reset your password</h2>
                   <p style='color:#666;margin-bottom:24px;'>Enter this OTP to reset your password. It expires in <strong>10 minutes</strong>.</p>
                   <div style='background:#f4f6fb;border-radius:12px;padding:24px;text-align:center;margin-bottom:24px;'>
                     <span style='font-size:40px;font-weight:700;letter-spacing:12px;font-family:monospace;'>{otpCode}</span>
                   </div>
                   <p style='color:#999;font-size:13px;'>If you didn't request this, ignore this email.</p>
                 </div>";

        await SendAsync(toEmail, subject, body);
    }

    private async Task SendAsync(string to, string subject, string htmlBody)
    {
        var provider = _config["Email:Provider"]?.Trim();
        if (string.Equals(provider, "Brevo", StringComparison.OrdinalIgnoreCase))
        {
            await SendWithBrevoAsync(to, subject, htmlBody);
            return;
        }

        if (!string.IsNullOrWhiteSpace(provider) &&
            !string.Equals(provider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported email provider '{provider}'.");
        }

        var host = _config["Email:SmtpHost"];

        if (string.IsNullOrWhiteSpace(host))
        {
            if (_env.IsDevelopment())
            {
                _logger.LogInformation("Email not sent in Development because SMTP is not configured. Subject: {Subject}", subject);
                return;
            }

            throw new InvalidOperationException("Email provider is not configured.");
        }

        var port = int.TryParse(_config["Email:SmtpPort"], out var p) ? p : 587;
        var username = _config["Email:Username"] ?? string.Empty;
        var password = _config["Email:Password"] ?? string.Empty;
        var fromAddress = _config["Email:FromAddress"] ?? username;
        var fromName = _config["Email:FromName"] ?? "Invoicely";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(username, password),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);

        try
        {
            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent to {Email}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", to);
            throw new InvalidOperationException("Could not send email. Please try again.", ex);
        }
    }

    private async Task SendWithBrevoAsync(string to, string subject, string htmlBody)
    {
        var apiKey = _config["Email:ApiKey"];
        var fromAddress = _config["Email:FromAddress"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Email:ApiKey is required when Email:Provider is Brevo.");
        if (string.IsNullOrWhiteSpace(fromAddress))
            throw new InvalidOperationException("Email:FromAddress must be a verified Brevo sender.");

        var payload = new
        {
            sender = new { name = _config["Email:FromName"] ?? "Invoicely", email = fromAddress },
            to = new[] { new { email = to } },
            subject,
            htmlContent = htmlBody
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.Add("api-key", apiKey);
        request.Headers.Accept.ParseAdd("application/json");
        request.Content = JsonContent.Create(payload, options: new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var client = _httpClientFactory?.CreateClient("transactional-email") ?? new HttpClient();
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Brevo rejected an email with HTTP status {StatusCode}.", (int)response.StatusCode);
            throw new InvalidOperationException($"Email provider rejected the message (HTTP {(int)response.StatusCode}).");
        }

        _logger.LogInformation("Email sent through Brevo to {Email}: {Subject}", to, subject);
    }

    public async Task SendAssistantInviteAsync(string toEmail, string assistantName, string businessName, string defaultPassword)
    {
        var loginUrl = BuildAssistantInviteLoginUrl(_config);
        var subject = $"You've been invited to work on {businessName} — Invoicely";
        var body = $@"<div style='font-family:Inter,sans-serif;max-width:480px;margin:0 auto;padding:32px;'>
           <h2 style='margin-bottom:8px;'>You've been added as an assistant</h2>
           <p style='color:#666;'>Hi {assistantName},</p>
           <p style='color:#666;'><strong>{businessName}</strong> has added you as an assistant on Invoicely.</p>
           <div style='background:#f4f6fb;border-radius:12px;padding:20px;margin:20px 0;'>
             <p style='margin:0 0 8px;font-weight:600;'>Your login details:</p>
             <p style='margin:0;'>Email: <strong>{toEmail}</strong></p>
             <p style='margin:4px 0 0;'>Password: <strong>{defaultPassword}</strong> (your phone number)</p>
           </div>
           <p style='color:#666;'>Please log in and change your password after your first sign-in.</p>
           <a href='{loginUrl}' style='display:inline-block;background:#4f7cff;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:600;margin-top:8px;'>Log in to Invoicely</a>
           <p style='color:#999;font-size:13px;margin-top:20px;'>If you weren't expecting this, you can ignore this email.</p>
         </div>";
        await SendAsync(toEmail, subject, body);
    }

    public static string BuildAssistantInviteLoginUrl(IConfiguration config)
    {
        var frontendBaseUrl = config["Email:FrontendBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            throw new InvalidOperationException("Email:FrontendBaseUrl is not configured.");

        return new Uri(new Uri(frontendBaseUrl + "/", UriKind.Absolute), "login").AbsoluteUri;
    }

}
