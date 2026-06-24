using Microsoft.EntityFrameworkCore;
using InvoicesBackend.Domain.Entities;
using InvoicesBackend.Domain.Enums;
using InvoicesBackend.Persistence;

namespace InvoicesBackend.Services;

/// <summary>
/// Generates and persists notifications for all businesses based on
/// current data. Called by the background job once per day.
/// Categories:
///   bill_return_due     — refundable bill returns due in ≤2 days
///   bill_return_overdue — refundable bill return is past its return date
///   invoice_due_soon    — invoice due in ≤3 days, not fully paid
///   invoice_overdue     — invoice past due date, not fully paid
///   assistant_unpaid    — assistant assignments unpaid
///   upcoming_project    — invoice line item dated tomorrow
/// </summary>
public class NotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ApplicationDbContext context, ILogger<NotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task GenerateAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var in2Days = today.AddDays(2);
        var in3Days = today.AddDays(3);

        var businesses = await _context.Businesses.ToListAsync();

        foreach (var business in businesses)
        {
            try
            {
                await GenerateForBusinessAsync(business, today, tomorrow, in2Days, in3Days);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed generating notifications for business {BusinessId}", business.Id);
            }
        }
    }

    private async Task GenerateForBusinessAsync(
        Business business, DateTime today, DateTime tomorrow, DateTime in2Days, DateTime in3Days)
    {
        var toAdd = new List<Notification>();

        // ---- Bill items: return due within 2 days ----
        var pendingReturnItems = await _context.BillItems
            .Include(i => i.Bill)
            .Where(i => i.Bill!.BusinessId == business.Id
                && i.IsRefundable
                && i.QuantityPending > 0)
            .ToListAsync();

        foreach (var item in pendingReturnItems)
        {
            if (!item.ReturnByDate.HasValue) continue;
            var returnDate = item.ReturnByDate.Value.Date;
            var bill = item.Bill!;

            if (returnDate < today)
            {
                if (!await ExistsAsync(business.Id, "bill_return_overdue", item.Id))
                {
                    toAdd.Add(new Notification
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = business.Id,
                        Type = "bill_return_overdue",
                        Title = "Return Overdue",
                        Message = $"{item.Quantity - item.QuantityReturned - item.QuantityBoughtByClient} unit(s) of \"{item.ItemName}\" from {bill.BrandName} ({bill.ProjectName}) were due for return on {returnDate:dd MMM yyyy}.",
                        LinkPath = "/app/bills",
                        RelatedEntityId = item.Id
                    });
                }
            }
            else if (returnDate <= in2Days)
            {
                if (!await ExistsAsync(business.Id, "bill_return_due", item.Id))
                {
                    toAdd.Add(new Notification
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = business.Id,
                        Type = "bill_return_due",
                        Title = "Return Due Soon",
                        Message = $"Return \"{item.ItemName}\" ({bill.BrandName} / {bill.ProjectName}) by {returnDate:dd MMM yyyy} ({(returnDate == today ? "today" : returnDate == tomorrow ? "tomorrow" : "in 2 days")}).",
                        LinkPath = "/app/bills",
                        RelatedEntityId = item.Id
                    });
                }
            }
        }

        // ---- Invoices: overdue or due soon ----
        var unpaidInvoices = await _context.Invoices
            .Where(i => i.BusinessId == business.Id
                && i.PaymentStatus != PaymentStatus.Paid
                && i.PaymentStatus != PaymentStatus.Cancelled)
            .ToListAsync();

        foreach (var inv in unpaidInvoices)
        {
            var dueDate = inv.DueDate.Date;

            if (dueDate < today)
            {
                if (!await ExistsAsync(business.Id, "invoice_overdue", inv.Id))
                {
                    toAdd.Add(new Notification
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = business.Id,
                        Type = "invoice_overdue",
                        Title = "Invoice Overdue",
                        Message = $"Invoice {inv.InvoiceNumber} was due on {dueDate:dd MMM yyyy} and still has ₹{inv.RemainingAmount:N0} outstanding.",
                        LinkPath = "/app/invoices",
                        RelatedEntityId = inv.Id
                    });
                }
            }
            else if (dueDate <= in3Days)
            {
                if (!await ExistsAsync(business.Id, "invoice_due_soon", inv.Id))
                {
                    toAdd.Add(new Notification
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = business.Id,
                        Type = "invoice_due_soon",
                        Title = "Invoice Due Soon",
                        Message = $"Invoice {inv.InvoiceNumber} (₹{inv.RemainingAmount:N0} remaining) is due on {dueDate:dd MMM yyyy}.",
                        LinkPath = "/app/invoices",
                        RelatedEntityId = inv.Id
                    });
                }
            }
        }

        // ---- Assistants: any unpaid assignments ----
        var unpaidAssistants = await _context.AssistantAssignments
            .Where(a => a.BusinessId == business.Id && !a.IsPaid && a.Fee > 0)
            .Join(_context.Assistants, a => a.AssistantId, x => x.Id, (a, x) => new { a, x.Name })
            .ToListAsync();

        foreach (var row in unpaidAssistants)
        {
            if (!await ExistsAsync(business.Id, "assistant_unpaid", row.a.Id))
            {
                toAdd.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    BusinessId = business.Id,
                    Type = "assistant_unpaid",
                    Title = "Assistant Payment Pending",
                    Message = $"{row.Name} has an unpaid fee of ₹{row.a.Fee:N0} for project \"{row.a.ProjectName}\".",
                    LinkPath = "/app/assistants",
                    RelatedEntityId = row.a.Id
                });
            }
        }

        // ---- Upcoming projects: line items dated tomorrow ----
        var upcomingItems = await (
            from item in _context.InvoiceItems
            join inv in _context.Invoices on item.InvoiceId equals inv.Id
            where inv.BusinessId == business.Id && item.ItemDate.Date == tomorrow
            select new { item.ProjectName, item.ExpenseName, inv.InvoiceNumber }
        ).ToListAsync();

        foreach (var it in upcomingItems)
        {
            var projectLabel = !string.IsNullOrWhiteSpace(it.ProjectName) ? it.ProjectName : it.ExpenseName;
            toAdd.Add(new Notification
            {
                Id = Guid.NewGuid(),
                BusinessId = business.Id,
                Type = "upcoming_project",
                Title = "Project Tomorrow",
                Message = $"\"{projectLabel}\" is scheduled for tomorrow (Invoice {it.InvoiceNumber}).",
                LinkPath = "/app/calendar"
            });
        }

        if (toAdd.Any())
        {
            _context.Notifications.AddRange(toAdd);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Generated {Count} notifications for business {BusinessId}", toAdd.Count, business.Id);
        }
    }

    private async Task<bool> ExistsAsync(Guid businessId, string type, Guid relatedId)
    {
        var since = DateTime.UtcNow.AddHours(-20); // don't re-generate within same day
        return await _context.Notifications.AnyAsync(n =>
            n.BusinessId == businessId &&
            n.Type == type &&
            n.RelatedEntityId == relatedId &&
            n.CreatedAt >= since);
    }
}
