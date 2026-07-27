using InvoicesBackend.Domain.Enums;
namespace InvoicesBackend.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    public string? FullName { get; set; }

    public string? Username { get; set; }

    public string? Email { get; set; }

    public string? PasswordHash { get; set; }

    /// <summary>"google" | "facebook" | "apple" | null for email/password accounts.</summary>
    public string? AuthProvider { get; set; }

    /// <summary>The unique subject/user id from the external provider.</summary>
    public string? ExternalId { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsMFAEnabled { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserRole Role { get; set; } = UserRole.BusinessOwner;

    public SubscriptionPlan SubscriptionPlan { get; set; } = SubscriptionPlan.Trial;

    public DateTime SubscriptionExpiryDate { get; set; } = DateTime.UtcNow.AddDays(14);
}