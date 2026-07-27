namespace InvoicesBackend.Application.DTOs;

public class AssistantResponse
{
    public string? Email { get; set; }
    public bool IsNewAccount { get; set; }
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int TotalAssignments { get; set; }
    public decimal TotalUnpaid { get; set; }
}

public class CreateAssistantRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class CreateAssignmentRequest
{
    public Guid? ProjectId { get; set; }
    public Guid? AssistantId { get; set; }
    public string? NewAssistantName { get; set; }
    public string? NewAssistantEmail { get; set; }
    public string? NewAssistantPhone { get; set; }

    public string ProjectName { get; set; } = string.Empty;
    public List<DateTime> WorkDates { get; set; } = new();
    public decimal Fee { get; set; }
    public bool IsPaid { get; set; }
    public string? Notes { get; set; }
}

public class AssignmentResponse
{
    public Guid Id { get; set; }
    public Guid AssistantId { get; set; }
    public string AssistantName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public List<DateTime> WorkDates { get; set; } = new();
    public decimal Fee { get; set; }
    public bool IsPaid { get; set; }
    public string? Notes { get; set; }
}
