namespace InvoicesBackend.Domain.Entities;

/// <summary>
/// Stores a business's uploaded "preferred invoice look" PDF, plus the
/// analyzed field/table layout used to overlay invoice data onto it.
/// One row per business (the latest upload replaces the previous one).
/// </summary>
public class InvoiceTemplate
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>The original uploaded PDF — used as the visual background for every generated invoice.</summary>
    public byte[] PdfData { get; set; } = Array.Empty<byte>();

    /// <summary>JSON-serialized InvoiceTemplateEngine.Models.InvoiceTemplateDefinition.</summary>
    public string TemplateJson { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
