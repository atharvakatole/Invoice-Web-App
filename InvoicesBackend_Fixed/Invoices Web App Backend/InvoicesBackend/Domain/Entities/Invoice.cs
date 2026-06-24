using InvoicesBackend.Domain.Enums;

namespace InvoicesBackend.Domain.Entities;

public class Invoice
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Guid ClientId { get; set; }
    public Guid? ProjectId { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    public decimal SubTotal { get; set; }
    public bool GSTIncluded { get; set; }
    public decimal GSTPercentage { get; set; }
    public decimal GSTAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; } = 0;
    public decimal RemainingAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string InvoiceStatus { get; set; } = "Draft";
    public string? Notes { get; set; }
    public bool IsClosed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}