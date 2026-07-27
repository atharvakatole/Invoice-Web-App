namespace InvoicesBackend.Domain.Entities;

public class Business
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string? BusinessName { get; set; }

    public string? BusinessEmail { get; set; }

    public string? BusinessPhone { get; set; }

    public string? BusinessAddress { get; set; } = string.Empty;

    public string? GSTNumber { get; set; }

    public string? LogoUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}