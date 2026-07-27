using System.Net;
using System.Net.Mail;

namespace InvoicesBackend.Services;

/// <summary>
/// Sends transactional emails. Configure SMTP in appsettings.json:
///   "Email": {
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

    public EmailService(IConfiguration config, ILogger<EmailService> logger, IWebHostEnvironment env)
    {
        _config = config;
        _logger = logger;
        _env = env;
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
        var host = _config["Email:SmtpHost"];

        // Only skip sending if SmtpHost is not configured
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogInformation(
                "──── EMAIL (NO SMTP CONFIG) ────\nTo: {To}\nSubject: {Subject}\nBody preview: {Body}\n─────────────────────────",
                to, subject, htmlBody[..Math.Min(200, htmlBody.Length)]);
            return;
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
    public async Task SendAssistantInviteAsync(string toEmail, string assistantName, string businessName, string defaultPassword)
    {
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
           <a href='http://localhost:4200/login' style='display:inline-block;background:#4f7cff;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:600;margin-top:8px;'>Log in to Invoicely</a>
           <p style='color:#999;font-size:13px;margin-top:20px;'>If you weren't expecting this, you can ignore this email.</p>
         </div>";
        await SendAsync(toEmail, subject, body);
    }

}
