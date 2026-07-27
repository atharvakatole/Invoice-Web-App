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
    private readonly PlanGuardService _planGuard;
    public InvoiceController(ApplicationDbContext context, PdfService pdfService, ExcelService excelService, ILogger<InvoiceController> logger, PlanGuardService planGuard)
    {
        _context = context;
        _pdfService = pdfService;
        _excelService = excelService;
        _logger = logger;
        _planGuard = planGuard;
    }

    [Authorize]
    [HttpPost("create")]
    public async Task<IActionResult> CreateInvoice(CreateInvoiceRequest request)
    {
        
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized();

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user token");

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user == null)
            return Unauthorized();

        if (business != null)
        {
            var invoiceLimitError = await _planGuard.CheckInvoiceLimitAsync(business.Id, userId);
            if (invoiceLimitError != null) return BadRequest(invoiceLimitError);
        }

        if (business == null)
            return BadRequest("Business not found");

        Client? client = null;
        if (request.ProjectId.HasValue)
        {
            var linkedProject = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId.Value && p.BusinessId == business.Id);

            if (linkedProject == null)
                return BadRequest("Project not found");

            client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == linkedProject.ClientId && c.BusinessId == business.Id);

            if (client == null)
                return BadRequest("Project client not found");

            if (string.IsNullOrWhiteSpace(request.ClientEmail))
            {
                request.ClientName = client.ClientName;
                request.ClientEmail = client.ClientEmail;
                request.ClientPhone = client.ClientPhone;
                request.ClientAddress = client.ClientAddress;
            }
        }

        if (client == null && !string.IsNullOrWhiteSpace(request.ClientEmail))
        {
            var clientEmail = request.ClientEmail.Trim().ToLowerInvariant();
            client = await _context.Clients
                .FirstOrDefaultAsync(x =>
                    x.ClientEmail != null &&
                    x.ClientEmail.ToLower() == clientEmail &&
                    x.BusinessId == business.Id);
        }

        if (client == null)
        {
            if (string.IsNullOrWhiteSpace(request.ClientName))
                return BadRequest("Client name is required");

            client = new Client
            {
                Id = Guid.NewGuid(),
                BusinessId = business.Id,
                ClientName = request.ClientName.Trim(),
                ClientEmail = string.IsNullOrWhiteSpace(request.ClientEmail) ? string.Empty : request.ClientEmail.Trim().ToLowerInvariant(),
                ClientPhone = request.ClientPhone,
                ClientAddress = request.ClientAddress
            };

            _context.Clients.Add(client);
        }

        if (request.Items.Count == 0)
            return BadRequest("At least one invoice item is required");

        if (request.Items.Any(x => x.Amount < 0 || x.Quantity <= 0))
            return BadRequest("Invoice item amounts must be non-negative and quantities must be greater than zero");

        decimal subtotal = request.Items.Sum(x => x.Amount * x.Quantity);
        decimal gstAmount = request.GSTIncluded ? subtotal * request.GSTPercentage / 100 : 0;
        decimal totalAmount = subtotal + gstAmount;

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
    public async Task<IActionResult> GetExpenseSuggestions(string? search)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized();

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user token");

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");

        var term = search?.Trim() ?? string.Empty;

        var suggestions = await _context.ExpenseMasters
                        .Where(x =>
                            x.BusinessId == business.Id &&
                            x.ExpenseName != null &&
                            EF.Functions.ILike(
                                x.ExpenseName,
                                $"%{term}%"))
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

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user token");
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

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user token");
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

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user token");

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

        if (invoice.PaymentStatus == PaymentStatus.Draft)
            return BadRequest("Draft invoices cannot accept payments");

        if (request.AmountPaid > invoice.RemainingAmount)
            return BadRequest("Payment amount cannot exceed the remaining invoice balance");

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

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user token");

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

        // Exclude Draft invoices from financial stats — they are not committed yet.
        var activeInvoices = invoices
            .Where(x => x.PaymentStatus != PaymentStatus.Draft)
            .ToList();

        var pendingRevenue = activeInvoices
            .Where(x => x.PaymentStatus != PaymentStatus.Paid)
            .Sum(x => x.RemainingAmount);

        var totalInvoices = activeInvoices.Count;

        var paidInvoices = activeInvoices.Count(x =>
            x.PaymentStatus == PaymentStatus.Paid);

        var pendingInvoices = activeInvoices.Count(x =>
            x.PaymentStatus != PaymentStatus.Paid && x.PaymentStatus != PaymentStatus.Cancelled);

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

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user token");

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
    public async Task<IActionResult> GetGstSummary([FromQuery] int? year)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized("User not found");
        if (!Guid.TryParse(userIdClaim, out var userId)) return Unauthorized("Invalid user token");

        var business = await _context.Businesses.FirstOrDefaultAsync(x => x.UserId == userId);
        if (business == null) return BadRequest("Business not found");

        var selectedYear = year ?? DateTime.UtcNow.Year;
        var currentYear  = DateTime.UtcNow.Year;
        var currentMonth = DateTime.UtcNow.Month;
        var currentQ     = (currentMonth - 1) / 3 + 1;

        // Load all non-draft invoices for the selected year
        var invoices = await _context.Invoices
            .Where(x => x.BusinessId == business.Id
                && x.PaymentStatus != Domain.Enums.PaymentStatus.Draft
                && x.InvoiceDate.Year == selectedYear)
            .ToListAsync();

        // Load bills + assistant fees + personal expenses for cost calculation
        var bills = await _context.Bills
            .Include(b => b.Items)
            .Where(b => b.BusinessId == business.Id
                && b.BillDate.Year == selectedYear)
            .ToListAsync();

        var assignments = await _context.AssistantAssignments
            .Where(a => a.BusinessId == business.Id)
            .ToListAsync();

        var personalExp = await _context.PersonalExpenses
            .Where(e => e.BusinessId == business.Id
                && e.ExpenseDate.Year == selectedYear)
            .ToListAsync();

        // ── Helpers ──────────────────────────────────────────
        decimal InvoiceRevenue(IEnumerable<Invoice> inv) => inv.Sum(i => i.AmountPaid);
        decimal InvoiceInvoiced(IEnumerable<Invoice> inv) => inv.Sum(i => i.TotalAmount);
        decimal InvoiceGST(IEnumerable<Invoice> inv) => inv.Where(i => i.GSTIncluded).Sum(i => i.GSTAmount);
        decimal BillCost(IEnumerable<Bill> b) => b.SelectMany(x => x.Items).Sum(i => i.TotalCost - i.AmountRefunded);
        decimal AssistantCost(IEnumerable<AssistantAssignment> a) => a.Sum(x => x.Fee);
        decimal PersonalCost(IEnumerable<PersonalExpense> p) => p.Sum(e => e.Amount);

        // ── Year totals ───────────────────────────────────────
        decimal yearInvoiced    = InvoiceInvoiced(invoices);
        decimal yearCollected   = InvoiceRevenue(invoices);
        decimal yearGST         = InvoiceGST(invoices);
        decimal yearBillCost    = BillCost(bills);
        decimal yearAssistFee   = AssistantCost(assignments
            .Where(a => a.WorkDates.Any(d => d.Year == selectedYear)));
        decimal yearPersonalExp = PersonalCost(personalExp);
        decimal yearTotalCost   = yearBillCost + yearAssistFee + yearPersonalExp;
        decimal yearProfit      = yearCollected - yearTotalCost;
        decimal yearMargin      = yearInvoiced > 0 ? Math.Round((yearProfit / yearInvoiced) * 100, 1) : 0;

        // ── Monthly breakdown ─────────────────────────────────
        var monthlyBreakdown = Enumerable.Range(1, 12).Select(m =>
        {
            var mInv  = invoices.Where(i => i.InvoiceDate.Month == m).ToList();
            var mBill = bills.Where(b => b.BillDate.Month == m).ToList();
            var mExp  = personalExp.Where(e => e.ExpenseDate.Month == m).ToList();
            var mAsgn = assignments.Where(a => a.WorkDates.Any(d => d.Year == selectedYear && d.Month == m)).ToList();
            decimal cost = BillCost(mBill) + AssistantCost(mAsgn) + PersonalCost(mExp);
            decimal rev  = InvoiceRevenue(mInv);
            return new
            {
                Month        = m,
                Invoiced     = InvoiceInvoiced(mInv),
                Collected    = rev,
                GSTCollected = InvoiceGST(mInv),
                TotalCosts   = cost,
                Profit       = rev - cost,
                Margin       = InvoiceInvoiced(mInv) > 0 ? Math.Round(((rev - cost) / InvoiceInvoiced(mInv)) * 100, 1) : 0m,
                Revenue      = InvoiceInvoiced(mInv) // kept for backward compat
            };
        }).ToList();

        // ── Quarterly breakdown ───────────────────────────────
        var quarterlyBreakdown = Enumerable.Range(1, 4).Select(q =>
        {
            var months = new[] { q * 3 - 2, q * 3 - 1, q * 3 };
            var qInv   = invoices.Where(i => months.Contains(i.InvoiceDate.Month)).ToList();
            var qBill  = bills.Where(b => months.Contains(b.BillDate.Month)).ToList();
            var qExp   = personalExp.Where(e => months.Contains(e.ExpenseDate.Month)).ToList();
            var qAsgn  = assignments.Where(a => a.WorkDates.Any(d => d.Year == selectedYear && months.Contains(d.Month))).ToList();
            decimal cost = BillCost(qBill) + AssistantCost(qAsgn) + PersonalCost(qExp);
            decimal rev  = InvoiceRevenue(qInv);
            decimal inv  = InvoiceInvoiced(qInv);
            return new
            {
                Quarter      = q,
                Label        = $"Q{q}",
                Invoiced     = inv,
                Collected    = rev,
                GSTCollected = InvoiceGST(qInv),
                TotalCosts   = cost,
                Profit       = rev - cost,
                Margin       = inv > 0 ? Math.Round(((rev - cost) / inv) * 100, 1) : 0m
            };
        }).ToList();

        // ── All-time totals (for the header) ──────────────────
        var allInvoices = await _context.Invoices
            .Where(x => x.BusinessId == business.Id && x.PaymentStatus != Domain.Enums.PaymentStatus.Draft)
            .ToListAsync();

        return Ok(new
        {
            SelectedYear        = selectedYear,
            AvailableYears      = allInvoices.Select(i => i.InvoiceDate.Year).Distinct().OrderByDescending(y => y).ToList(),
            TotalGSTCollected   = allInvoices.Where(x => x.GSTIncluded).Sum(x => x.GSTAmount),

            Year = new
            {
                Invoiced     = yearInvoiced,
                Collected    = yearCollected,
                GSTCollected = yearGST,
                TotalCosts   = yearTotalCost,
                Profit       = yearProfit,
                Margin       = yearMargin
            },

            QuarterlySummary = quarterlyBreakdown,
            MonthlySummary   = monthlyBreakdown
        });
    }

    [Authorize]
    [HttpGet("export-fiscal-year")]
    public async Task<IActionResult> ExportFiscalYear()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("User not found");

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user token");

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

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user token");

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

    /// <summary>
    /// Returns the draft invoice (and items) linked to a given project.
    /// Used by invoice-create to pre-populate items from bill sells.
    /// </summary>
    [Authorize]
    [HttpGet("draft-for-project")]
    public async Task<IActionResult> GetDraftForProject(
        [FromQuery] string? projectName,
        [FromQuery] Guid? projectId)
    {
        var userIdClaim2 = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim2, out var userId2))
            return Unauthorized("Invalid token");

        var business2 = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId2);
        if (business2 == null) return BadRequest("Business not found");

        Invoice? draft = null;

        if (projectId.HasValue)
        {
            draft = await _context.Invoices
                .FirstOrDefaultAsync(i =>
                    i.BusinessId == business2.Id &&
                    i.PaymentStatus == PaymentStatus.Draft &&
                    i.ProjectId == projectId.Value);

            // If no draft found by ProjectId FK, resolve project name and fall through
            if (draft == null && string.IsNullOrWhiteSpace(projectName))
            {
                var proj = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId.Value && p.BusinessId == business2.Id);
                if (proj != null) projectName = proj.Name;
            }
        }

        if (draft == null && !string.IsNullOrWhiteSpace(projectName))
        {
            var name = projectName.Trim().ToLower();
            var draftIds = await _context.InvoiceItems
                .Where(item => item.ProjectName != null &&
                    item.ProjectName.ToLower() == name)
                .Select(item => item.InvoiceId)
                .Distinct()
                .ToListAsync();

            if (draftIds.Any())
            {
                draft = await _context.Invoices
                    .FirstOrDefaultAsync(i =>
                        i.BusinessId == business2.Id &&
                        i.PaymentStatus == PaymentStatus.Draft &&
                        draftIds.Contains(i.Id));
            }
        }

        if (draft == null) return NotFound();

        // Fetch items from the separate InvoiceItems table
        var draftItems = await _context.InvoiceItems
            .Where(i => i.InvoiceId == draft.Id)
            .ToListAsync();

        // Fetch client info
        var draftClient = await _context.Clients
            .FirstOrDefaultAsync(cl => cl.Id == draft.ClientId && cl.BusinessId == business2.Id);

        return Ok(new
        {
            draft.Id,
            draft.ClientId,
            ClientName  = draftClient?.ClientName ?? string.Empty,
            ClientEmail = draftClient?.ClientEmail ?? string.Empty,
            ClientPhone = draftClient?.ClientPhone ?? string.Empty,
            ClientAddress = draftClient?.ClientAddress ?? string.Empty,
            draft.TotalAmount,
            Items = draftItems.Select(i => new
            {
                i.ExpenseName,
                i.ProjectName,
                i.Amount,
                i.Quantity,
                i.ItemDate
            }).ToList()
        });
    }

    /// <summary>
    /// Promotes a draft invoice to Pending and clears DraftInvoiceId on linked BillItems.
    /// Call this when the user finalises the create form with items from a draft.
    /// </summary>
    [Authorize]
    [HttpPut("{invoiceId}/finalise-draft")]
    public async Task<IActionResult> FinaliseDraft(Guid invoiceId)
    {
        var userIdClaim3 = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim3, out var userId3))
            return Unauthorized("Invalid token");

        var business3 = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId3);
        if (business3 == null) return BadRequest("Business not found");

        var invoice3 = await _context.Invoices
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.BusinessId == business3.Id);

        if (invoice3 == null) return NotFound();
        if (invoice3.PaymentStatus != PaymentStatus.Draft)
            return BadRequest("Invoice is not a draft");

        invoice3.PaymentStatus = PaymentStatus.Pending;
        invoice3.InvoiceStatus = "Active";

        // Remove draft link on BillItems — they now have a real invoice
        var linkedBillItems = await _context.BillItems
            .Where(bi => bi.DraftInvoiceId == invoiceId)
            .ToListAsync();
        foreach (var bi in linkedBillItems) bi.DraftInvoiceId = null;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Draft finalised", invoiceId });
    }


[HttpGet]
[Authorize]
public async Task<IActionResult> GetInvoices()
{
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("User not found");

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user token");

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

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user token");

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

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user token");

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");

        var ev = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            Title = request.Title.Trim(),
            EventDate = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Utc),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        // Resolve ProjectId: use explicit ID first, otherwise look up by name
        if (request.ProjectId.HasValue)
        {
            ev.ProjectId = request.ProjectId;
        }
        else if (!string.IsNullOrWhiteSpace(request.ProjectName))
        {
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.BusinessId == business.Id
                    && p.Name == request.ProjectName.Trim());
            ev.ProjectId = project?.Id;
        }

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

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user token");

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

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user token");

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (business == null)
            return BadRequest("Business not found");

        var cutoff = DateTime.UtcNow.AddMonths(-4);

        // Include Projects table so any project shows up even if no invoice/event yet
        var fromProjects = await _context.Projects
            .Where(p => p.BusinessId == business.Id && p.Status != Domain.Entities.ProjectStatus.Archived)
            .Select(p => new { Name = p.Name, Date = p.CreatedAt })
            .ToListAsync();

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

        var combined = fromProjects
            .Concat(fromItems)
            .Concat(fromEvents.Select(e => new { Name = e.Title, e.Date }))
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
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
