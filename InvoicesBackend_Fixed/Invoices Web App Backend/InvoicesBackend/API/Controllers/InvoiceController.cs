using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using InvoicesBackend.Persistence;
using InvoicesBackend.Application.DTOs;
using InvoicesBackend.Domain.Entities;
using InvoicesBackend.Services;
using System.Security.Claims;
using InvoicesBackend.Domain.Enums;
using InvoiceTemplateEngine;
using InvoiceTemplateEngine.Models;
using System.Text.Json;
namespace InvoicesBackend.API.Controllers;


[ApiController]
[Route("api/invoices")]
public class InvoiceController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly PdfService _pdfService;
    private readonly ExcelService _excelService;
    private readonly ILogger<InvoiceController> _logger;
    public InvoiceController(ApplicationDbContext context, PdfService pdfService, ExcelService excelService, ILogger<InvoiceController> logger)
    {
        _context = context;
        _pdfService = pdfService;
        _excelService = excelService;
        _logger = logger;
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("create")]
    public async Task<IActionResult> CreateInvoice(CreateInvoiceRequest request)
    {
        
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized();

        var userId = Guid.Parse(userIdClaim);

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user == null)
            return Unauthorized();

        if (user.SubscriptionPlan == SubscriptionPlan.Free)
        {
            if (business == null)
                return BadRequest("Business not found");
            var invoiceCount = await _context.Invoices
                .CountAsync(x => x.BusinessId == business.Id);

            if (invoiceCount >= 5)
            {
                return BadRequest("Free plan invoice limit reached. Please upgrade to Premium.");
            }
        }

        if (business == null)
            return BadRequest("Business not found");
        var client = await _context.Clients
                    .FirstOrDefaultAsync(x =>
                        x.ClientEmail == request.ClientEmail &&
                        x.BusinessId == business.Id);

        if (client == null)
        {
            client = new Client
            {
                Id = Guid.NewGuid(),
                BusinessId = business.Id,
                ClientName = request.ClientName,
                ClientEmail = request.ClientEmail,
                ClientPhone = request.ClientPhone,
                ClientAddress = request.ClientAddress
            };

            _context.Clients.Add(client);
        }

        decimal subtotal = request.Items.Sum(x => x.Amount * x.Quantity);

        decimal gstAmount = 0;

        if (request.GSTIncluded)
        {
            gstAmount = subtotal * request.GSTPercentage / 100;
        }

        decimal totalAmount = subtotal + gstAmount;

        // If a ProjectId is provided, resolve client from it
        if (request.ProjectId.HasValue)
        {
            var linkedProject = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId.Value && p.BusinessId == business.Id);
            if (linkedProject != null && string.IsNullOrWhiteSpace(request.ClientEmail))
            {
                var linkedClient = await _context.Clients
                    .FirstOrDefaultAsync(c => c.Id == linkedProject.ClientId);
                if (linkedClient != null)
                {
                    request.ClientName = linkedClient.ClientName;
                    request.ClientEmail = linkedClient.ClientEmail;
                    request.ClientPhone = linkedClient.ClientPhone;
                    request.ClientAddress = linkedClient.ClientAddress;
                }
            }
        }

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            ClientId = client.Id,
            ProjectId = request.ProjectId,
            InvoiceNumber = $"INV-{DateTime.UtcNow.Ticks}",
            DueDate = DateTime.SpecifyKind(request.DueDate, DateTimeKind.Utc),
            SubTotal = subtotal,
            GSTIncluded = request.GSTIncluded,
            GSTPercentage = request.GSTPercentage,
            GSTAmount = gstAmount,
            TotalAmount = totalAmount,
            PaymentStatus = PaymentStatus.Pending,
            RemainingAmount = totalAmount,
            AmountPaid = 0,
            IsClosed = false,
            Notes = request.Notes
        };

        _context.Invoices.Add(invoice);

        foreach (var item in request.Items)
        {
            var existingExpense = await _context.ExpenseMasters
                                .FirstOrDefaultAsync(x =>
                                x.ExpenseName == item.ExpenseName &&
                                x.BusinessId == business.Id);
            if (existingExpense == null)
            {
                existingExpense = new ExpenseMaster
                {
                    Id = Guid.NewGuid(),
                    BusinessId = business.Id,
                    ExpenseName = item.ExpenseName
                };

                _context.ExpenseMasters.Add(existingExpense);
            }
            else
            {
                existingExpense.LastUsedDate = DateTime.UtcNow;
            }
            var itemDate = item.ItemDate == default
                ? invoice.InvoiceDate
                : DateTime.SpecifyKind(item.ItemDate, DateTimeKind.Utc);

            var invoiceItem = new InvoiceItem
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                ExpenseName = item.ExpenseName,
                ProjectName = string.IsNullOrWhiteSpace(item.ProjectName) ? null : item.ProjectName.Trim(),
                ItemDate = itemDate,
                Amount = item.Amount,
                Quantity = item.Quantity,
                Total = item.Amount * item.Quantity
            };

            _context.InvoiceItems.Add(invoiceItem);
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Invoice created successfully",
            InvoiceNumber = invoice.InvoiceNumber,
            TotalAmount = totalAmount
        });
    }

    [Authorize]
    [HttpGet("expense-suggestions")]
    public async Task<IActionResult> GetExpenseSuggestions(string search)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized();

        var userId = Guid.Parse(userIdClaim);

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");

        var suggestions = await _context.ExpenseMasters
                        .Where(x =>
                            x.BusinessId == business.Id &&
                            EF.Functions.ILike(
                                x.ExpenseName,
                                $"%{search}%"))
                        .OrderByDescending(x => x.LastUsedDate)
                        .Select(x => x.ExpenseName)
                        .Take(10)
                        .ToListAsync();
        return Ok(suggestions);
    }

    /* [HttpGet("download-pdf/{invoiceId}")]
    public async Task<IActionResult> DownloadPdfTest(Guid invoiceId)
    {
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(x => x.Id == invoiceId);

        if (invoice == null)
            return NotFound("Invoice not found");

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.Id == invoice.BusinessId);

        var client = await _context.Clients
            .FirstOrDefaultAsync(x => x.Id == invoice.ClientId);

        var items = await _context.InvoiceItems
            .Where(x => x.InvoiceId == invoice.Id)
            .ToListAsync();

        var pdfBytes = await GenerateInvoicePdfBytesAsync(
            business,
            client,
            invoice,
            items);

        return File(
            pdfBytes,
            "application/pdf",
            $"{invoice.InvoiceNumber}.pdf");
    }*/
    
    [Authorize]
    [HttpGet("preview-pdf/{invoiceId}")]
    public async Task<IActionResult> PreviewPdf(Guid invoiceId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("User not found");

        var userId = Guid.Parse(userIdClaim);
        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");
            
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(x =>
                x.Id == invoiceId &&
                x.BusinessId == business.Id);

        if (invoice == null)
            return NotFound();

        /*var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.Id == invoice.BusinessId);*/

        var client = await _context.Clients
            .FirstOrDefaultAsync(x => x.Id == invoice.ClientId);
        
        if(client == null)
            return BadRequest("Client not found");

        var items = await _context.InvoiceItems
            .Where(x => x.InvoiceId == invoice.Id)
            .ToListAsync();

        try
        {
            var pdfBytes = await GenerateInvoicePdfBytesAsync(
                business,
                client,
                invoice,
                items);

            return File(
                pdfBytes,
                "application/pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF preview for invoice {InvoiceId}", invoice.Id);
            return Problem(
                title: "Could not generate the invoice PDF",
                detail: "Something went wrong while rendering this invoice. Please try again, or contact support if the problem persists.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("download-pdf/{invoiceId}")]
    [Authorize]
    public async Task<IActionResult> DownloadPdf(Guid invoiceId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("User not found");

        var userId = Guid.Parse(userIdClaim);
        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");
            
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(x =>
                x.Id == invoiceId &&
                x.BusinessId == business.Id);

        /*var invoice = await _context.Invoices
            .FirstOrDefaultAsync(x => x.Id == invoiceId);*/

        if (invoice == null)
            return NotFound();

        var client = await _context.Clients
            .FirstOrDefaultAsync(x => x.Id == invoice.ClientId);
            
        if(client == null)
            return BadRequest("Client not found");

        var items = await _context.InvoiceItems
            .Where(x => x.InvoiceId == invoice.Id)
            .ToListAsync();

        try
        {
            var pdfBytes = await GenerateInvoicePdfBytesAsync(
                business,
                client,
                invoice,
                items);

            return File(
                pdfBytes,
                "application/pdf",
                $"{invoice.InvoiceNumber}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF download for invoice {InvoiceId}", invoice.Id);
            return Problem(
                title: "Could not generate the invoice PDF",
                detail: "Something went wrong while rendering this invoice. Please try again, or contact support if the problem persists.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
    [Authorize]
    [HttpPut("update-payment/{invoiceId}")]
    public async Task<IActionResult> UpdatePayment(
        Guid invoiceId,
        UpdatePaymentRequest request)
    {
        if (request.AmountPaid <= 0)
            return BadRequest("Amount paid must be greater than zero");

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("User not found");

        var userId = Guid.Parse(userIdClaim);

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");

        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(x =>
                x.Id == invoiceId &&
                x.BusinessId == business.Id);

        if (invoice == null)
            return NotFound("Invoice not found");

        invoice.AmountPaid += request.AmountPaid;

        invoice.RemainingAmount =
            invoice.TotalAmount - invoice.AmountPaid;

        if (invoice.AmountPaid >= invoice.TotalAmount)
        {
            invoice.PaymentStatus = PaymentStatus.Paid;
            invoice.IsClosed = true;
            invoice.RemainingAmount = 0;
        }
        else if (invoice.AmountPaid > 0)
        {
            invoice.PaymentStatus = PaymentStatus.PartiallyPaid;
            invoice.IsClosed = false;
        }
        else
        {
            invoice.PaymentStatus = PaymentStatus.Pending;
            invoice.IsClosed = false;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Payment updated successfully",
            invoice.PaymentStatus,
            invoice.AmountPaid,
            invoice.RemainingAmount,
            invoice.IsClosed
        });
    }

    [Authorize]
    [HttpGet("dashboard-summary")]
    public async Task<IActionResult> GetDashboardSummary()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("User not found");

        var userId = Guid.Parse(userIdClaim);

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");

        var currentYear = DateTime.UtcNow.Year;

        var invoices = await _context.Invoices
            .Where(x => x.BusinessId == business.Id)
            .ToListAsync();

        // For recent invoices: client names + project names
        var allClientIds = invoices.Select(i => i.ClientId).Distinct().ToList();
        var clientsDict = await _context.Clients
            .Where(c => allClientIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.ClientName ?? string.Empty);

        var allInvoiceIds = invoices.Select(i => i.Id).ToList();
        var topProjectsRaw = await _context.InvoiceItems
            .Where(x => allInvoiceIds.Contains(x.InvoiceId) && x.ProjectName != null && x.ProjectName != "")
            .GroupBy(x => x.InvoiceId)
            .Select(g => new { InvoiceId = g.Key, ProjectName = g.OrderByDescending(x => x.ItemDate).First().ProjectName })
            .ToListAsync();
        var projectDict = topProjectsRaw.ToDictionary(x => x.InvoiceId, x => x.ProjectName ?? string.Empty);

        var totalRevenue = invoices
            .Where(x => x.PaymentStatus == PaymentStatus.Paid)
            .Sum(x => x.TotalAmount);

        var pendingRevenue = invoices
            .Where(x => x.PaymentStatus != PaymentStatus.Paid)
            .Sum(x => x.RemainingAmount);

        var totalInvoices = invoices.Count;

        var paidInvoices = invoices.Count(x =>
            x.PaymentStatus == PaymentStatus.Paid);

        var pendingInvoices = invoices.Count(x =>
            x.PaymentStatus != PaymentStatus.Paid);

        var monthlyRevenue = invoices
            .Where(x =>
                x.InvoiceDate.Year == currentYear &&
                x.PaymentStatus == PaymentStatus.Paid)
            .GroupBy(x => x.InvoiceDate.Month)
            .Select(g => new
            {
                Month = g.Key,
                Revenue = g.Sum(x => x.TotalAmount)
            })
            .OrderBy(x => x.Month)
            .ToList();

        // Invoice status distribution (for a chart)
        var statusDistribution = invoices
            .GroupBy(x => x.PaymentStatus)
            .Select(g => new
            {
                Status = g.Key.ToString(),
                Count = g.Count(),
                Total = g.Sum(x => x.TotalAmount)
            })
            .OrderByDescending(g => g.Count)
            .ToList();

        // Active clients = clients invoiced in the last 90 days
        var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);
        var activeClients = invoices
            .Where(x => x.InvoiceDate >= ninetyDaysAgo)
            .Select(x => x.ClientId)
            .Distinct()
            .Count();

        var totalClients = await _context.Clients
            .CountAsync(c => c.BusinessId == business.Id);

        // Top clients by total billed
        var clients = await _context.Clients
            .Where(c => c.BusinessId == business.Id)
            .ToListAsync();

        var topClients = invoices
            .GroupBy(x => x.ClientId)
            .Select(g => new
            {
                ClientId = g.Key,
                ClientName = clients.FirstOrDefault(c => c.Id == g.Key)?.ClientName ?? "Unknown",
                TotalBilled = g.Sum(x => x.TotalAmount),
                InvoiceCount = g.Count()
            })
            .OrderByDescending(x => x.TotalBilled)
            .Take(5)
            .ToList();

        // Assistant payments
        var assistantAssignments = await _context.AssistantAssignments
            .Where(a => a.BusinessId == business.Id)
            .ToListAsync();

        var pendingAssistantPayments = assistantAssignments
            .Where(a => !a.IsPaid)
            .Sum(a => a.Fee);

        var paidAssistantPayments = assistantAssignments
            .Where(a => a.IsPaid)
            .Sum(a => a.Fee);

        // Upcoming projects: invoice items with a future date
        var now = DateTime.UtcNow.Date;
        var upcomingProjects = await (
            from item in _context.InvoiceItems
            join invoice in _context.Invoices on item.InvoiceId equals invoice.Id
            where invoice.BusinessId == business.Id && item.ItemDate.Date >= now
            select item
        ).Select(i => i.ItemDate.Date).Distinct().CountAsync();

        return Ok(new
        {
            TotalRevenue = totalRevenue,
            PendingRevenue = pendingRevenue,
            TotalInvoices = totalInvoices,
            PaidInvoices = paidInvoices,
            PendingInvoices = pendingInvoices,
            MonthlyRevenue = monthlyRevenue,
            StatusDistribution = statusDistribution,
            ActiveClients = activeClients,
            TotalClients = totalClients,
            TopClients = topClients,
            PendingAssistantPayments = pendingAssistantPayments,
            PaidAssistantPayments = paidAssistantPayments,
            UpcomingProjectDays = upcomingProjects,
            RecentInvoices = invoices
                .OrderByDescending(i => i.InvoiceDate)
                .Take(5)
                .Select(i => new
                {
                    i.Id,
                    i.InvoiceNumber,
                    ClientName = clientsDict.TryGetValue(i.ClientId, out var cn) ? cn : string.Empty,
                    ProjectName = projectDict.TryGetValue(i.Id, out var pn) ? pn : string.Empty,
                    i.InvoiceDate,
                    i.TotalAmount,
                    i.PaymentStatus
                })
        });
    }

    [Authorize]
    [HttpGet("client-ledger/{clientId}")]
    public async Task<IActionResult> GetClientLedger(Guid clientId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("User not found");

        var userId = Guid.Parse(userIdClaim);

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");

        var client = await _context.Clients
            .FirstOrDefaultAsync(x =>
                x.Id == clientId &&
                x.BusinessId == business.Id);

        if (client == null)
            return NotFound("Client not found");

        var invoices = await _context.Invoices
            .Where(x =>
                x.ClientId == clientId &&
                x.BusinessId == business.Id)
            .OrderByDescending(x => x.InvoiceDate)
            .ToListAsync();

        var totalBilled = invoices.Sum(x => x.TotalAmount);
        var totalPaid = invoices.Sum(x => x.AmountPaid);
        var pendingAmount = invoices.Sum(x => x.RemainingAmount);

        return Ok(new
        {
            Client = new
            {
                client.Id,
                client.ClientName,
                client.ClientEmail,
                client.ClientPhone
            },

            Summary = new
            {
                TotalInvoices = invoices.Count,
                TotalBilled = totalBilled,
                TotalPaid = totalPaid,
                PendingAmount = pendingAmount
            },

            Invoices = invoices.Select(x => new
            {
                x.Id,
                x.InvoiceNumber,
                x.InvoiceDate,
                x.TotalAmount,
                x.AmountPaid,
                x.RemainingAmount,
                x.PaymentStatus,
                x.IsClosed
            })
        });
    }

    [Authorize]
    [HttpGet("gst-summary")]
    public async Task<IActionResult> GetGstSummary()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("User not found");

        var userId = Guid.Parse(userIdClaim);

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");

        var currentYear = DateTime.UtcNow.Year;

        var invoices = await _context.Invoices
            .Where(x => x.BusinessId == business.Id)
            .ToListAsync();

        var totalGstCollected = invoices
            .Where(x => x.GSTIncluded)
            .Sum(x => x.GSTAmount);

        var monthlyGstSummary = invoices
            .Where(x =>
                x.InvoiceDate.Year == currentYear &&
                x.GSTIncluded)
            .GroupBy(x => x.InvoiceDate.Month)
            .Select(g => new
            {
                Month = g.Key,
                GSTCollected = g.Sum(x => x.GSTAmount),
                Revenue = g.Sum(x => x.TotalAmount)
            })
            .OrderBy(x => x.Month)
            .ToList();

        return Ok(new
        {
            TotalGSTCollected = totalGstCollected,
            MonthlySummary = monthlyGstSummary
        });
    }

    [Authorize]
    [HttpGet("export-fiscal-year")]
    public async Task<IActionResult> ExportFiscalYear()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("User not found");

        var userId = Guid.Parse(userIdClaim);

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");

        var invoices = await _context.Invoices
            .Where(x => x.BusinessId == business.Id)
            .OrderByDescending(x => x.InvoiceDate)
            .ToListAsync();

        var fileBytes = _excelService.GenerateFiscalYearReport(invoices);

        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"FiscalYearReport-{DateTime.UtcNow.Year}.xlsx");
    }

    [Authorize]
    [HttpGet("export-fiscal-year-pdf")]
    public async Task<IActionResult> ExportFiscalYearPdf()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("User not found");

        var userId = Guid.Parse(userIdClaim);

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");

        var invoices = await _context.Invoices
            .Where(x => x.BusinessId == business.Id)
            .OrderByDescending(x => x.InvoiceDate)
            .ToListAsync();

        var pdfBytes = _pdfService.GenerateFiscalYearReportPdf(
            business,
            invoices);

        return File(
            pdfBytes,
            "application/pdf",
            $"FiscalYearReport-{DateTime.UtcNow.Year}.pdf");
    }

[HttpGet]
[Authorize]
public async Task<IActionResult> GetInvoices()
{
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("User not found");

        var userId = Guid.Parse(userIdClaim);

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");

        var invoices = await _context.Invoices
            .Where(x => x.BusinessId == business.Id)
            .OrderByDescending(x => x.InvoiceDate)
            .ToListAsync();

        var clientIds = invoices.Select(i => i.ClientId).Distinct().ToList();
        var clients = await _context.Clients
            .Where(c => clientIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.ClientName ?? string.Empty);

        var invoiceIds = invoices.Select(i => i.Id).ToList();
        var topProjects = await _context.InvoiceItems
            .Where(x => invoiceIds.Contains(x.InvoiceId) && x.ProjectName != null && x.ProjectName != "")
            .GroupBy(x => x.InvoiceId)
            .Select(g => new { InvoiceId = g.Key, ProjectName = g.OrderByDescending(x => x.ItemDate).First().ProjectName })
            .ToListAsync();

        var projectMap = topProjects.ToDictionary(x => x.InvoiceId, x => x.ProjectName ?? string.Empty);

        var result = invoices.Select(i => new
        {
            i.Id,
            i.InvoiceNumber,
            ClientName = clients.TryGetValue(i.ClientId, out var cn) ? cn : string.Empty,
            ProjectName = projectMap.TryGetValue(i.Id, out var pn) ? pn : string.Empty,
            i.InvoiceDate,
            i.DueDate,
            i.TotalAmount,
            i.AmountPaid,
            i.RemainingAmount,
            i.PaymentStatus,
            i.IsClosed
        });

    return Ok(result);
}

    /// <summary>
    /// Generates the invoice PDF, in priority order:
    /// 1. A fully custom uploaded PDF template (<see cref="InvoiceTemplate"/>) — overlay rendering.
    /// 2. "Design it yourself" branding (<see cref="InvoiceBranding"/>) — generated layout with logo/colors/footer.
    /// 3. The default built-in layout.
    /// </summary>
    private async Task<byte[]> GenerateInvoicePdfBytesAsync(
        Business business,
        Client client,
        Invoice invoice,
        List<InvoiceItem> items)
    {
        var template = await _context.InvoiceTemplates
            .FirstOrDefaultAsync(x => x.BusinessId == business.Id);

        if (template == null)
        {
            var branding = await _context.InvoiceBrandings
                .FirstOrDefaultAsync(x => x.BusinessId == business.Id);

            if (branding != null)
            {
                try
                {
                    return _pdfService.GenerateBrandedInvoicePdf(business, client, invoice, items, branding);
                }
                catch
                {
                    return _pdfService.GenerateInvoicePdf(business, client, invoice, items);
                }
            }

            return _pdfService.GenerateInvoicePdf(business, client, invoice, items);
        }

        try
        {
            var definition = JsonSerializer.Deserialize<InvoiceTemplateDefinition>(template.TemplateJson);
            if (definition == null)
            {
                return _pdfService.GenerateInvoicePdf(business, client, invoice, items);
            }

            var renderModel = new InvoiceRenderModel
            {
                BusinessName = business.BusinessName ?? string.Empty,
                BusinessEmail = business.BusinessEmail ?? string.Empty,
                BusinessPhone = business.BusinessPhone ?? string.Empty,
                BusinessAddress = business.BusinessAddress ?? string.Empty,
                GSTNumber = business.GSTNumber ?? string.Empty,

                ClientName = client.ClientName ?? string.Empty,
                ClientEmail = client.ClientEmail ?? string.Empty,
                ClientPhone = client.ClientPhone ?? string.Empty,
                ClientAddress = client.ClientAddress ?? string.Empty,

                InvoiceNumber = invoice.InvoiceNumber ?? string.Empty,
                InvoiceDate = invoice.InvoiceDate,
                DueDate = invoice.DueDate,
                PaymentStatus = invoice.PaymentStatus.ToString(),
                Notes = invoice.Notes ?? string.Empty,

                Items = items.Select(i => new InvoiceLineItemModel
                {
                    Name = i.ExpenseName ?? string.Empty,
                    Rate = i.Amount,
                    Quantity = i.Quantity,
                    Total = i.Total
                }).ToList(),

                SubTotal = invoice.SubTotal,
                GSTIncluded = invoice.GSTIncluded,
                GSTPercentage = invoice.GSTPercentage,
                GSTAmount = invoice.GSTAmount,
                TotalAmount = invoice.TotalAmount,
                AmountPaid = invoice.AmountPaid,
                RemainingAmount = invoice.RemainingAmount
            };

            return InvoiceTemplateRenderer.Render(template.PdfData, definition, renderModel);
        }
        catch
        {
            // If anything goes wrong with the custom template, never block
            // the user from getting their invoice — fall back to default.
            return _pdfService.GenerateInvoicePdf(business, client, invoice, items);
        }
    }

    [Authorize]
    [HttpGet("calendar-events")]
    public async Task<IActionResult> GetCalendarEvents([FromQuery] int year, [FromQuery] int month)
    {
        if (month < 1 || month > 12)
            return BadRequest("Month must be between 1 and 12");

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized();

        var userId = Guid.Parse(userIdClaim);

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");

        var rangeStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var rangeEnd = rangeStart.AddMonths(1);

        var events = new List<CalendarEventResponse>();

        // Invoice line items (project work) within this month
        var itemRows = await (
            from item in _context.InvoiceItems
            join invoice in _context.Invoices on item.InvoiceId equals invoice.Id
            where invoice.BusinessId == business.Id
                  && item.ItemDate >= rangeStart && item.ItemDate < rangeEnd
            select new { item.ItemDate, item.ExpenseName, item.ProjectName, item.Total, invoice.InvoiceNumber }
        ).ToListAsync();

        events.AddRange(itemRows.Select(r => new CalendarEventResponse
        {
            Date = r.ItemDate,
            Type = "invoice-item",
            Title = !string.IsNullOrWhiteSpace(r.ProjectName) ? r.ProjectName! : (r.ExpenseName ?? "Project"),
            Subtitle = !string.IsNullOrWhiteSpace(r.ProjectName) ? $"{r.ExpenseName} · Invoice {r.InvoiceNumber}" : $"Invoice {r.InvoiceNumber}",
            Amount = r.Total
        }));

        // Assistant assignments with work dates in this month
        var assignments = await _context.AssistantAssignments
            .Where(a => a.BusinessId == business.Id)
            .Join(_context.Assistants, a => a.AssistantId, x => x.Id, (a, x) => new { a, x.Name })
            .ToListAsync();

        foreach (var a in assignments)
        {
            foreach (var date in a.a.WorkDates.Where(d => d >= rangeStart && d < rangeEnd))
            {
                events.Add(new CalendarEventResponse
                {
                    Date = date,
                    Type = "assistant",
                    Title = a.a.ProjectName,
                    Subtitle = $"Assistant: {a.Name}",
                    Amount = a.a.Fee,
                    IsPaid = a.a.IsPaid,
                    RelatedId = a.a.Id
                });
            }
        }

        // Manually added planner events (projects/shoots/meetings/deadlines)
        var manualEvents = await _context.CalendarEvents
            .Where(e => e.BusinessId == business.Id && e.EventDate >= rangeStart && e.EventDate < rangeEnd)
            .ToListAsync();

        events.AddRange(manualEvents.Select(e => new CalendarEventResponse
        {
            Date = e.EventDate,
            Type = "project",
            Title = e.Title,
            Subtitle = e.Notes,
            RelatedId = e.Id
        }));

        return Ok(events.OrderBy(e => e.Date).ToList());
    }

    [Authorize]
    [HttpPost("calendar-events")]
    public async Task<IActionResult> CreateCalendarEvent(CreateCalendarEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required");

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized();

        var userId = Guid.Parse(userIdClaim);

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");

        var ev = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            ProjectId = request.ProjectId,
            Title = request.Title.Trim(),
            EventDate = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Utc),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        _context.CalendarEvents.Add(ev);
        await _context.SaveChangesAsync();

        return Ok(new CalendarEventResponse
        {
            Date = ev.EventDate,
            Type = "project",
            Title = ev.Title,
            Subtitle = ev.Notes,
            RelatedId = ev.Id
        });
    }

    [Authorize]
    [HttpDelete("calendar-events/{id}")]
    public async Task<IActionResult> DeleteCalendarEvent(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized();

        var userId = Guid.Parse(userIdClaim);

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");

        var ev = await _context.CalendarEvents
            .FirstOrDefaultAsync(e => e.Id == id && e.BusinessId == business.Id);

        if (ev == null)
            return NotFound();

        _context.CalendarEvents.Remove(ev);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Event removed" });
    }

    /// <summary>
    /// Recently-used project names (from invoice line items and manually
    /// added calendar events) within the last 4 months, most recent first —
    /// used to power the "Project" autocomplete when creating an invoice.
    /// </summary>
    [Authorize]
    [HttpGet("project-suggestions")]
    public async Task<IActionResult> GetProjectSuggestions([FromQuery] string? q)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized();

        var userId = Guid.Parse(userIdClaim);

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");

        var cutoff = DateTime.UtcNow.AddMonths(-4);

        var fromItems = await (
            from item in _context.InvoiceItems
            join invoice in _context.Invoices on item.InvoiceId equals invoice.Id
            where invoice.BusinessId == business.Id
                  && item.ProjectName != null && item.ProjectName != ""
                  && item.ItemDate >= cutoff
            select new { Name = item.ProjectName!, Date = item.ItemDate }
        ).ToListAsync();

        var fromEvents = await _context.CalendarEvents
            .Where(e => e.BusinessId == business.Id && e.EventDate >= cutoff)
            .Select(e => new { e.Title, Date = e.EventDate })
            .ToListAsync();

        var combined = fromItems
            .Concat(fromEvents.Select(e => new { Name = e.Title, e.Date }))
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new ProjectSuggestionResponse
            {
                Name = g.First().Name.Trim(),
                LastUsed = g.Max(x => x.Date)
            })
            .OrderByDescending(x => x.LastUsed)
            .ToList();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            combined = combined.Where(x => x.Name.ToLowerInvariant().Contains(term)).ToList();
        }

        return Ok(combined.Take(10).ToList());
    }
}