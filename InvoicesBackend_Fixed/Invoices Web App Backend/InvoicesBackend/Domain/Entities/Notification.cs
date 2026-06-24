namespace InvoicesBackend.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    /// <summary>
    /// "invoice_overdue" | "invoice_due_soon" | "bill_return_due" |
    /// "assistant_unpaid" | "upcoming_project"
    /// </summary>
    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    /// <summary>
    /// Optional deep-link path shown in the frontend (e.g. "/app/bills").
    /// </summary>
    public string? LinkPath { get; set; }

    public Guid? RelatedEntityId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
