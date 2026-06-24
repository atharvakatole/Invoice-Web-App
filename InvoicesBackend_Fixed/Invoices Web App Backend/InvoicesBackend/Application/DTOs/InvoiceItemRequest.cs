namespace InvoicesBackend.Application.DTOs;

public class InvoiceItemRequest
{
    public string ExpenseName { get; set; } = string.Empty;

    public string? ProjectName { get; set; }

    public DateTime ItemDate { get; set; }

    public decimal Amount { get; set; }

    public int Quantity { get; set; }
}