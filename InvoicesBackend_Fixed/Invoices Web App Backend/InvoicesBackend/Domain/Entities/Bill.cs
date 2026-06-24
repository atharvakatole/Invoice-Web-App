namespace InvoicesBackend.Domain.Entities;

/// <summary>
/// Header for an out-of-pocket expense trip to a brand (e.g. buying/renting
/// clothes for a shoot). Contains brand, project, date and payment method.
/// Actual items purchased are in <see cref="BillItem"/>.
/// </summary>
public class Bill
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Guid? ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Project? Project { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
    public string PaidWith { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<BillItem> Items { get; set; } = new();
}
