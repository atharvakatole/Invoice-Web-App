namespace InvoicesBackend.Application.DTOs;

public class InviteAssistantRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public class AssistantLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RespondToAssignmentRequest
{
    /// <summary>"accept" or "reject"</summary>
    public string Response { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class AssistantReturnRequest
{
    public Guid BillItemId { get; set; }
    public Guid AssignmentId { get; set; }
    public int QuantityToReturn { get; set; }
    public string? Notes { get; set; }
}

public class ResolveReturnRequest
{
    /// <summary>"approve" or "reject"</summary>
    public string Resolution { get; set; } = string.Empty;
    public string? ManagerNotes { get; set; }
}
