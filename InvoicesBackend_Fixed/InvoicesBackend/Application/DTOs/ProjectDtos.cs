namespace InvoicesBackend.Application.DTOs;

public class ProjectResponse
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Budget { get; set; }
    public string? Notes { get; set; }
    public int InvoiceCount { get; set; }
    public int AssistantCount { get; set; }
    public int BillCount { get; set; }
    public decimal TotalInvoiced { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateProjectRequest
{
    public Guid ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Budget { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProjectRequest : CreateProjectRequest
{
    public string Status { get; set; } = "Active";
}
