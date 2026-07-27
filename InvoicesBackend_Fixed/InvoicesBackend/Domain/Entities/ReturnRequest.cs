namespace InvoicesBackend.Domain.Entities;
using InvoicesBackend.Domain.Enums;
/// <summary>
/// When an assistant wants to return a rented/refundable bill item,
/// they submit a ReturnRequest. The manager must approve before it is
/// actually marked as returned in BillItem.
/// </summary>
public class ReturnRequest
{
    public Guid Id { get; set; }

    public Guid BillItemId { get; set; }
    public BillItem? BillItem { get; set; }

    /// <summary>The AssistantAssignment this return is linked to.</summary>
    public Guid AssignmentId { get; set; }
    public AssistantAssignment? Assignment { get; set; }

    /// <summary>The User account of the assistant who submitted this request.</summary>
    public Guid AssistantUserId { get; set; }

    public int QuantityToReturn { get; set; }

    public string? Notes { get; set; }

    /// <summary>"Pending" | "Approved" | "Rejected"</summary>
    public ReturnRequestStatus Status { get; set; } = ReturnRequestStatus.Pending;

    public string? ManagerNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
