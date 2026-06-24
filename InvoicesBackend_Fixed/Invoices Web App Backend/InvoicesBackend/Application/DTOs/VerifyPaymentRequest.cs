namespace InvoicesBackend.Application.DTOs;

public class VerifyPaymentRequest
{
    public string? RazorpayPaymentId { get; set; }

    public string? RazorpayOrderId { get; set; }

    public string? RazorpaySignature { get; set; }
}