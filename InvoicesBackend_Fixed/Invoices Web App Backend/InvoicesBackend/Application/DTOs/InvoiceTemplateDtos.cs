namespace InvoicesBackend.Application.DTOs;

public class InvoiceTemplateStatusResponse
{
    public bool HasTemplate { get; set; }
    public string? FileName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<string> DetectedFields { get; set; } = new();
    public List<string> MissingFields { get; set; } = new();
}
