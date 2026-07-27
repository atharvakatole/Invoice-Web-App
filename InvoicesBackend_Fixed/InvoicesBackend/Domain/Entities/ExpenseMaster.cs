namespace InvoicesBackend.Domain.Entities;

public class ExpenseMaster
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public string? ExpenseName { get; set; }

    public DateTime LastUsedDate { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}