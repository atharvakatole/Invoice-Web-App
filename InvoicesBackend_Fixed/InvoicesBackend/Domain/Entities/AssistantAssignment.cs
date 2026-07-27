using InvoicesBackend.Domain.Enums;
namespace InvoicesBackend.Domain.Entities;

/// <summary>
/// Tracks an assistant's involvement on a project: which days they worked,
/// what fee they're owed, and whether they've been paid yet.
/// </summary>
public class AssistantAssignment
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public Guid AssistantId { get; set; }

    public Guid? ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Project? Project { get; set; }

    /// <summary>The specific dates the assistant was hired for this project.</summary>
    public List<DateTime> WorkDates { get; set; } = new();

    public decimal Fee { get; set; }

    public bool IsPaid { get; set; }

    public string? Notes { get; set; }

    /// <summary>Pending/Accepted/Rejected/Completed</summary>
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Pending;

    /// <summary>The manager (BusinessOwner) who created this assignment.</summary>
    public Guid? AddedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
