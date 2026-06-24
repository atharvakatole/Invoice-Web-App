using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicesBackend.Persistence;
using InvoicesBackend.Domain.Enums;
using InvoicesBackend.Application.DTOs;

namespace InvoicesBackend.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "SuperAdmin")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AdminController(ApplicationDbContext context,IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetAdminDashboard()
    {
        var totalUsers = await _context.Users.CountAsync();

        var totalBusinesses = await _context.Businesses.CountAsync();

        var totalInvoices = await _context.Invoices.CountAsync();

        var totalRevenue = await _context.Invoices
            .Where(x => x.PaymentStatus == PaymentStatus.Paid)
            .SumAsync(x => x.TotalAmount);

        var pendingRevenue = await _context.Invoices
            .Where(x => x.PaymentStatus != PaymentStatus.Paid)
            .SumAsync(x => x.RemainingAmount);

        var premiumUsers = await _context.Users
            .CountAsync(x => x.SubscriptionPlan == SubscriptionPlan.Premium);

        return Ok(new
        {
            TotalUsers = totalUsers,
            TotalBusinesses = totalBusinesses,
            TotalInvoices = totalInvoices,
            TotalRevenue = totalRevenue,
            PendingRevenue = pendingRevenue,
            PremiumUsers = premiumUsers
        });
    }

    [Authorize]
    [HttpPost("create-payment-order")]
    public IActionResult CreatePaymentOrder(
        CreatePaymentRequest request)
    {
        var client = new Razorpay.Api.RazorpayClient(
            _configuration["Razorpay:Key"],
            _configuration["Razorpay:Secret"]);

        var options = new Dictionary<string, object>
        {
            { "amount", (int)(request.Amount * 100) },
            { "currency", "INR" },
            { "receipt", Guid.NewGuid().ToString() },
            { "payment_capture", 1 }
        };

        var order = client.Order.Create(options);

        return Ok(new
        {
            OrderId = order["id"].ToString(),
            Amount = order["amount"].ToString(),
            Currency = order["currency"].ToString()
        });
    }
}