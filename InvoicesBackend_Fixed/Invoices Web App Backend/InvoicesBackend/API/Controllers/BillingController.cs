using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Razorpay.Api;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using InvoicesBackend.Persistence;
using InvoicesBackend.Application.DTOs;
using InvoicesBackend.Domain.Enums;

namespace InvoicesBackend.API.Controllers;

[ApiController]
[Route("api/billing")]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;

    public BillingController(
        IConfiguration configuration,
        ApplicationDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }

    [HttpGet("subscription-status")]
    public async Task<IActionResult> GetSubscriptionStatus()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

        var userId = Guid.Parse(userIdClaim);
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user == null) return Unauthorized();

        var isPremium = user.SubscriptionPlan == SubscriptionPlan.Premium;
        var isTrial = user.SubscriptionPlan == SubscriptionPlan.Trial;
        var isExpired = user.SubscriptionExpiryDate < DateTime.UtcNow;

        return Ok(new
        {
            Plan = user.SubscriptionPlan.ToString(),
            IsPremium = isPremium,
            IsTrial = isTrial,
            IsExpired = isExpired,
            ExpiryDate = user.SubscriptionExpiryDate,
            NextBillingDate = isPremium ? user.SubscriptionExpiryDate : (DateTime?)null,
            PremiumBenefits = new[]
            {
                "Unlimited invoices",
                "Unlimited clients",
                "Unlimited assistant assignments",
                "Custom invoice branding (logo, colors, layout)",
                "Custom PDF template upload",
                "Bills & expense tracker with outfit photos",
                "Full calendar & schedule planner",
                "Revenue and GST reports",
                "Priority support"
            }
        });
    }

    [HttpPost("create-order")]
    public IActionResult CreateOrder(CreatePaymentRequest request)
    {
        if (request.Amount < 1)
            return BadRequest("Minimum amount is ₹1");

        var client = new RazorpayClient(
            _configuration["Razorpay:Key"],
            _configuration["Razorpay:Secret"]);

        var options = new Dictionary<string, object>
        {
            { "amount", (int)(request.Amount * 100) },
            { "currency", "INR" },
            { "receipt", Guid.NewGuid().ToString() }
        };

        var order = client.Order.Create(options);

        return Ok(new
        {
            order_id = order["id"].ToString(),
            amount = order["amount"].ToString(),
            currency = order["currency"].ToString(),
            key = _configuration["Razorpay:Key"]
        });
    }

    [HttpPost("verify-payment")]
    public async Task<IActionResult> VerifyPayment(
        VerifyPaymentRequest request)
    {
        if (string.IsNullOrEmpty(request.RazorpayOrderId) ||
            string.IsNullOrEmpty(request.RazorpayPaymentId) ||
            string.IsNullOrEmpty(request.RazorpaySignature))
        {
            return BadRequest("Invalid payment details");
        }

        var generatedSignature = GenerateSignature(
            request.RazorpayOrderId,
            request.RazorpayPaymentId,
            _configuration["Razorpay:Secret"]!);

        if (generatedSignature.ToLower() !=
                request.RazorpaySignature.ToLower())
            return BadRequest("Payment verification failed");

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized();

        var userId = Guid.Parse(userIdClaim);

        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user == null)
            return Unauthorized();

        user.SubscriptionPlan = SubscriptionPlan.Premium;
        user.SubscriptionExpiryDate = DateTime.UtcNow.AddDays(30);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            Success = true,
            Message = "Payment verified. Welcome to Premium!",
            Plan = "Premium",
            IsPremium = true,
            NextBillingDate = user.SubscriptionExpiryDate
        });
    }

    private string GenerateSignature(
        string orderId,
        string paymentId,
        string secret)
    {
        var payload = $"{orderId}|{paymentId}";

        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(secret));

        var hash = hmac.ComputeHash(
            Encoding.UTF8.GetBytes(payload));

        return BitConverter
            .ToString(hash)
            .Replace("-", "")
            .ToLower();
    }
}