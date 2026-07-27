namespace InvoicesBackend.Domain.Entities;

/// <summary>
/// "Design it yourself" invoice branding — used to generate a polished
/// invoice PDF (via PdfService) when the business hasn't uploaded a fully
/// custom PDF template (<see cref="InvoiceTemplate"/>).
/// One row per business; the latest save replaces the previous one.
/// </summary>
public class InvoiceBranding
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    /// <summary>Layout preset: "modern" | "classic" | "minimal".</summary>
    public string TemplateStyle { get; set; } = "modern";

    /// <summary>Hex accent color, e.g. "#4F7CFF".</summary>
    public string AccentColor { get; set; } = "#4F7CFF";

    /// <summary>Optional uploaded logo image bytes (PNG/JPEG).</summary>
    public byte[]? LogoData { get; set; }

    public string? LogoContentType { get; set; }

    /// <summary>e.g. "Aishwarya Gupta".</summary>
    public string FooterName { get; set; } = string.Empty;

    /// <summary>e.g. "Stylist | Costume Designer".</summary>
    public string FooterTitle { get; set; } = string.Empty;

    /// <summary>e.g. "PAN : BTSPG3322H".</summary>
    public string FooterSubtitle { get; set; } = string.Empty;

    /// <summary>Free-form multi-line payment/bank details block.</summary>
    public string PaymentDetails { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
