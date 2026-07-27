namespace InvoicesBackend.Domain.Entities;

/// <summary>
/// Stores OTP codes for email verification during registration,
/// and reset tokens for password recovery.
/// Type: "registration" | "password_reset"
/// </summary>
public class OtpVerification
{
    public Guid Id { get; set; }

    /// <summary>Email this OTP was sent to.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>"registration" or "password_reset"</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>BCrypt hash of the 6-digit OTP, or SHA256 of the reset token.</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public int AttemptCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
