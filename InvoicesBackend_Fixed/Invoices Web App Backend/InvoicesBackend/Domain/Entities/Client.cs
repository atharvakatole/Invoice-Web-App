namespace InvoicesBackend.Domain.Entities;

public class Client
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public string? ClientName { get; set; }

    public string? ClientEmail { get; set; } = string.Empty;

    public string? ClientPhone { get; set; }

    public string? ClientAddress { get; set; } =  string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}