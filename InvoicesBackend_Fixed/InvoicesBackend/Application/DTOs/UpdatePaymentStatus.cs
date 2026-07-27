using InvoicesBackend.Domain.Enums;

namespace InvoicesBackend.Application.DTOs;

public class UpdatePaymentRequest
{
    public decimal AmountPaid { get; set; }

    public PaymentStatus PaymentStatus { get; set; }
}