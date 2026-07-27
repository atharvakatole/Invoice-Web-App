namespace InvoicesBackend.Domain.Enums;
public enum PaymentStatus
{
    Draft = 0,
    Pending = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Overdue = 4,
    Cancelled = 5
}