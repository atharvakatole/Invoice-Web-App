namespace InvoicesBackend.Domain.Entities;

/// <summary>
/// A user-added planner entry (project/shoot/meeting/deadline) shown on the
/// Schedule calendar, independent of invoice line items or assistant
/// assignments. Also feeds the "recent projects" suggestion list when
/// creating an invoice.
/// </summary>
public class CalendarEvent
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Project? Project { get; set; }

    public DateTime EventDate { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
