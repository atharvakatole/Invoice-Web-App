namespace InvoicesBackend.Application.DTOs;

public class InvoiceBrandingResponse
{
    public bool HasBranding { get; set; }
    public string TemplateStyle { get; set; } = "modern";
    public string AccentColor { get; set; } = "#4F7CFF";
    public bool HasLogo { get; set; }
    public string FooterName { get; set; } = string.Empty;
    public string FooterTitle { get; set; } = string.Empty;
    public string FooterSubtitle { get; set; } = string.Empty;
    public string PaymentDetails { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
}

public class TemplateStyleOption
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
