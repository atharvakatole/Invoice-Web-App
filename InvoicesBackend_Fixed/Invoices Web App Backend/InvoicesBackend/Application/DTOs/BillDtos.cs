namespace InvoicesBackend.Application.DTOs;

public class BillItemResponse
{
    public Guid Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal PricePerItem { get; set; }
    public decimal TotalCost { get; set; }
    public bool IsRefundable { get; set; }
    public DateTime? ReturnByDate { get; set; }
    public int QuantityReturned { get; set; }
    public decimal AmountRefunded { get; set; }
    public int QuantityBoughtByClient { get; set; }
    public decimal AmountBoughtByClient { get; set; }
    public string? BoughtByClientName { get; set; }
    public Guid? BoughtByClientId { get; set; }
    public Guid? DraftInvoiceId { get; set; }
    public int QuantityPending { get; set; }
    public bool HasImage { get; set; }
    public string? Notes { get; set; }
}

public class BillResponse
{
    public Guid Id { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
    public string PaidWith { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<BillItemResponse> Items { get; set; } = new();
    public decimal TotalCost { get; set; }
    public decimal TotalRefunded { get; set; }
    public decimal TotalBought { get; set; }
    public decimal TotalPending { get; set; }
}

public class BillItemRequest
{
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal PricePerItem { get; set; }
    public bool IsRefundable { get; set; }
    public DateTime? ReturnByDate { get; set; }
    public string? Notes { get; set; }
}

public class CreateBillRequest
{
    public Guid? ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
    public string PaidWith { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<BillItemRequest> Items { get; set; } = new();
}

public class ReturnItemsRequest
{
    public int QuantityToReturn { get; set; }
}

public class SellToClientRequest
{
    public int QuantityToSell { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
}
