namespace InvoicesBackend.Domain.Entities;

public class Assistant
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    /// <summary>Linked User account for assistant login. Null until invitation is accepted.</summary>
    public Guid? UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
