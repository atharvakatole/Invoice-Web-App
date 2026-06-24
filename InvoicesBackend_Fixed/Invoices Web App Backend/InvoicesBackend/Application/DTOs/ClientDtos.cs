namespace InvoicesBackend.Application.DTOs;

public class ClientSummaryResponse
{
    public Guid Id { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? ClientEmail { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientAddress { get; set; }
    public int InvoiceCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal PendingAmount { get; set; }
    public DateTime LastInvoiceDate { get; set; }
}

public class UpdateClientRequest
{
    public string ClientName { get; set; } = string.Empty;
    public string? ClientEmail { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientAddress { get; set; }
}

public class LastInvoiceItemResponse
{
    public string ExpenseName { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public decimal Amount { get; set; }
    public int Quantity { get; set; }
}
