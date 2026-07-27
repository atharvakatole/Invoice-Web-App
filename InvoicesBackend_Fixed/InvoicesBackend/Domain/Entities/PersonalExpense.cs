namespace InvoicesBackend.Domain.Entities;

public class PersonalExpense
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>e.g. Travel, Food, Equipment, Rent, Marketing, Other</summary>
    public string Category { get; set; } = "Other";

    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; }

    public string? Notes { get; set; }

    /// <summary>Optional project tag.</summary>
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
