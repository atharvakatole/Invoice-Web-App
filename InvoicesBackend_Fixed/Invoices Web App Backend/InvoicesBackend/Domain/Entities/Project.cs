namespace InvoicesBackend.Domain.Entities;

public enum ProjectStatus { Active, Completed, Archived }

/// <summary>
/// The central entity that ties a client engagement together.
/// Invoices, Bills, AssistantAssignments and CalendarEvents all hang off a Project.
/// </summary>
public class Project
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Guid ClientId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Budget { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Client? Client { get; set; }
    public List<Invoice> Invoices { get; set; } = new();
    public List<Bill> Bills { get; set; } = new();
    public List<AssistantAssignment> AssistantAssignments { get; set; } = new();
    public List<CalendarEvent> CalendarEvents { get; set; } = new();
}
