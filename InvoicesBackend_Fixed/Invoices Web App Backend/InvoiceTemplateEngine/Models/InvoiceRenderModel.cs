namespace InvoiceTemplateEngine.Models;

public class InvoiceLineItemModel
{
    public string Name { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public int Quantity { get; set; }
    public decimal Total { get; set; }
}

/// <summary>
/// All dynamic data needed to render an invoice — fully decoupled from
/// the EF entities so this library has no dependency on the API project.
/// </summary>
public class InvoiceRenderModel
{
    // Business
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessEmail { get; set; } = string.Empty;
    public string BusinessPhone { get; set; } = string.Empty;
    public string BusinessAddress { get; set; } = string.Empty;
    public string GSTNumber { get; set; } = string.Empty;

    // Client
    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public string ClientAddress { get; set; } = string.Empty;

    // Invoice
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public List<InvoiceLineItemModel> Items { get; set; } = new();

    public decimal SubTotal { get; set; }
    public bool GSTIncluded { get; set; }
    public decimal GSTPercentage { get; set; }
    public decimal GSTAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal RemainingAmount { get; set; }
}
