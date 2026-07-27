namespace InvoicesBackend.Domain.Entities;

public class InvoiceItem
{
    public Guid Id { get; set; }

    public Guid InvoiceId { get; set; }

    public string? ExpenseName { get; set; }

    public string? ProjectName { get; set; }

    public DateTime ItemDate { get; set; }

    public decimal Amount { get; set; }

    public int Quantity { get; set; }

    public decimal Total { get; set; }
}