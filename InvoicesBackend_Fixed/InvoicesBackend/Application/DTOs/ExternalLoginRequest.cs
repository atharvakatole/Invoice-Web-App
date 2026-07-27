namespace InvoicesBackend.Application.DTOs;

public class ExternalLoginRequest
{
    /// <summary>"google" | "facebook" | "apple"</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Google: the ID token (JWT) from Google Identity Services.
    /// Facebook: the access token from the Facebook Login SDK.
    /// Apple: the identity token (JWT) from "Sign in with Apple".
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Apple only sends the user's name on the *first* sign-in, separately
    /// from the identity token. The frontend should pass it through here
    /// if available so we can use it for new accounts.
    /// </summary>
    public string? FullName { get; set; }
}
