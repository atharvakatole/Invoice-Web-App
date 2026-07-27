namespace InvoicesBackend.Application.DTOs;

public class CalendarEventResponse
{
    public DateTime Date { get; set; }

    /// <summary>"invoice-item" | "assistant"</summary>
    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public decimal? Amount { get; set; }
    public bool? IsPaid { get; set; }
    public Guid? RelatedId { get; set; }
}

public class CreateCalendarEventRequest
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
}

public class ProjectSuggestionResponse
{
    public string Name { get; set; } = string.Empty;
    public DateTime LastUsed { get; set; }
}
