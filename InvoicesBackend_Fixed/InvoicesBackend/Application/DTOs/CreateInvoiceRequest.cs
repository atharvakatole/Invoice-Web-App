namespace InvoicesBackend.Application.DTOs;

public class CreateInvoiceRequest
{
    public Guid? ProjectId { get; set; }
    public string? ClientName { get; set; }

    public string? ClientEmail { get; set; }

    public string? ClientPhone { get; set; }

    public string? ClientAddress { get; set; }

    public DateTime DueDate { get; set; }

    public bool GSTIncluded { get; set; }

    public decimal GSTPercentage { get; set; }

    public string? Notes { get; set; }

    public List<InvoiceItemRequest> Items { get; set; } = new List<InvoiceItemRequest>();
}