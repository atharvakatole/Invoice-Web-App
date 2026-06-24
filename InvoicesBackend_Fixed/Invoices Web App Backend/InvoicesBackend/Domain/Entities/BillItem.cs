namespace InvoicesBackend.Domain.Entities;

/// <summary>
/// A single product line within a <see cref="Bill"/>.
/// Tracks quantity purchased, returns (partial or full), and whether the
/// client has opted to buy some of the items.
/// </summary>
public class BillItem
{
    public Guid Id { get; set; }
    public Guid BillId { get; set; }

    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal PricePerItem { get; set; }

    public bool IsRefundable { get; set; }
    public DateTime? ReturnByDate { get; set; }

    /// <summary>How many units have been returned to the brand so far.</summary>
    public int QuantityReturned { get; set; }

    /// <summary>How many units the client has opted to purchase (buy instead of return).</summary>
    public int QuantityBoughtByClient { get; set; }

    /// <summary>Name of the client who bought items (free text or linked).</summary>
    public string? BoughtByClientName { get; set; }

    /// <summary>Linked client id if selected from the client list.</summary>
    public Guid? BoughtByClientId { get; set; }

    /// <summary>
    /// When set, a draft invoice has been created for the client purchase.
    /// </summary>
    public Guid? DraftInvoiceId { get; set; }

    public string? Notes { get; set; }

    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Bill? Bill { get; set; }

    // ── Computed helpers (not persisted) ──────────────────────────────────
    public decimal TotalCost => Quantity * PricePerItem;
    public decimal AmountRefunded => QuantityReturned * PricePerItem;
    public decimal AmountBoughtByClient => QuantityBoughtByClient * PricePerItem;
    public int QuantityPending => Quantity - QuantityReturned - QuantityBoughtByClient;
}
