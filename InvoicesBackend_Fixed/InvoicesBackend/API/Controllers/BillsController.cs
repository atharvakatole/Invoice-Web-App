using InvoicesBackend.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicesBackend.Application.DTOs;
using InvoicesBackend.Domain.Entities;
using InvoicesBackend.Domain.Enums;
using InvoicesBackend.Persistence;

namespace InvoicesBackend.API.Controllers;

[ApiController]
[Route("api/bills")]
[Authorize]
public class BillsController : ControllerBase
{
    private static readonly string[] AllowedPaymentMethods =
        { "UPI", "Card", "Cash", "Bank Transfer", "Net Banking", "Other" };

    private readonly ApplicationDbContext _context;
    private readonly PlanGuardService _planGuard;

    public BillsController(ApplicationDbContext context, PlanGuardService planGuard) { _context = context; _planGuard = planGuard; }

    // ════════════════════════════════════════════════════════
    // BILLS (headers)
    // ════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> GetBills([FromQuery] string? project)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var query = _context.Bills
            .Include(b => b.Items)
            .Where(b => b.BusinessId == business.Id);

        if (!string.IsNullOrWhiteSpace(project))
            query = query.Where(b => b.ProjectName == project);

        var bills = await query.OrderByDescending(b => b.BillDate).ToListAsync();

        return Ok(bills.Select(ToResponse));
    }

    [HttpPost]
    public async Task<IActionResult> CreateBill(CreateBillRequest request)
    {
        var err = ValidateBill(request);
        if (err != null) return BadRequest(err);

        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var premUid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(premUid, out var premUidG))
        {
            var premErr = await _planGuard.RequirePremiumAsync(premUidG, "Bills");
            if (premErr != null) return BadRequest(premErr);
        }


        var bill = new Bill
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            ProjectId = request.ProjectId,
            ProjectName = request.ProjectName.Trim(),
            BrandName = request.BrandName.Trim(),
            BillDate = DateTime.SpecifyKind(request.BillDate.Date, DateTimeKind.Utc),
            PaidWith = request.PaidWith.Trim(),
            Notes = request.Notes?.Trim()
        };

        foreach (var itemReq in request.Items)
        {
            bill.Items.Add(new BillItem
            {
                Id = Guid.NewGuid(),
                BillId = bill.Id,
                ItemName = itemReq.ItemName.Trim(),
                Quantity = itemReq.Quantity,
                PricePerItem = itemReq.PricePerItem,
                IsRefundable = itemReq.IsRefundable,
                ReturnByDate = itemReq.IsRefundable && itemReq.ReturnByDate.HasValue
                    ? DateTime.SpecifyKind(itemReq.ReturnByDate.Value.Date, DateTimeKind.Utc)
                    : null,
                Notes = itemReq.Notes?.Trim()
            });
        }

        _context.Bills.Add(bill);
        await _context.SaveChangesAsync();

        return Ok(ToResponse(bill));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBill(Guid id, CreateBillRequest request)
    {
        var err = ValidateBill(request);
        if (err != null) return BadRequest(err);

        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var bill = await _context.Bills.Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == id && b.BusinessId == business.Id);
        if (bill == null) return NotFound();

        bill.ProjectId = request.ProjectId;
        bill.ProjectName = request.ProjectName.Trim();
        bill.BrandName = request.BrandName.Trim();
        bill.BillDate = DateTime.SpecifyKind(request.BillDate.Date, DateTimeKind.Utc);
        bill.PaidWith = request.PaidWith.Trim();
        bill.Notes = request.Notes?.Trim();

        await _context.SaveChangesAsync();
        return Ok(ToResponse(bill));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBill(Guid id)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var bill = await _context.Bills.Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == id && b.BusinessId == business.Id);
        if (bill == null) return NotFound();

        _context.Bills.Remove(bill);
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Bill removed" });
    }

    // ════════════════════════════════════════════════════════
    // BILL ITEMS — add / image / return / sell / delete
    // ════════════════════════════════════════════════════════

    [HttpPost("{billId}/items")]
    public async Task<IActionResult> AddItem(Guid billId, BillItemRequest request)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var bill = await _context.Bills.Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == billId && b.BusinessId == business.Id);
        if (bill == null) return NotFound();

        var item = new BillItem
        {
            Id = Guid.NewGuid(),
            BillId = billId,
            ItemName = request.ItemName.Trim(),
            Quantity = request.Quantity,
            PricePerItem = request.PricePerItem,
            IsRefundable = request.IsRefundable,
            ReturnByDate = request.IsRefundable && request.ReturnByDate.HasValue
                ? DateTime.SpecifyKind(request.ReturnByDate.Value.Date, DateTimeKind.Utc)
                : null,
            Notes = request.Notes?.Trim()
        };

        bill.Items.Add(item);
        await _context.SaveChangesAsync();
        return Ok(ToItemResponse(item));
    }

    [HttpDelete("{billId}/items/{itemId}")]
    public async Task<IActionResult> DeleteItem(Guid billId, Guid itemId)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var item = await _context.BillItems
            .FirstOrDefaultAsync(x => x.Id == itemId && x.BillId == billId);
        if (item == null) return NotFound();

        _context.BillItems.Remove(item);
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Item removed" });
    }

    // ── Image ────────────────────────────────────────────────

    [HttpGet("{billId}/items/{itemId}/image")]
    public async Task<IActionResult> GetItemImage(Guid billId, Guid itemId)
    {
        var item = await _context.BillItems
            .FirstOrDefaultAsync(x => x.Id == itemId && x.BillId == billId);

        if (item?.ImageData == null || item.ImageData.Length == 0)
            return NotFound();

        return File(item.ImageData, item.ImageContentType ?? "image/jpeg");
    }

    [HttpPost("{billId}/items/{itemId}/image")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadItemImage(Guid billId, Guid itemId, IFormFile image)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var item = await _context.BillItems
            .Include(x => x.Bill)
            .FirstOrDefaultAsync(x => x.Id == itemId && x.BillId == billId);

        if (item == null || item.Bill?.BusinessId != business.Id)
            return NotFound();

        if (image == null || image.Length == 0)
            return BadRequest("No image provided");

        if (image.ContentType != "image/jpeg" && image.ContentType != "image/png" && image.ContentType != "image/webp")
            return BadRequest("Image must be JPEG, PNG, or WebP");

        if (image.Length > 5 * 1024 * 1024)
            return BadRequest("Image must be under 5 MB");

        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        item.ImageData = ms.ToArray();
        item.ImageContentType = image.ContentType;
        await _context.SaveChangesAsync();

        return Ok(new { HasImage = true });
    }

    [HttpDelete("{billId}/items/{itemId}/image")]
    public async Task<IActionResult> DeleteItemImage(Guid billId, Guid itemId)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var item = await _context.BillItems
            .Include(x => x.Bill)
            .FirstOrDefaultAsync(x => x.Id == itemId && x.BillId == billId);

        if (item == null || item.Bill?.BusinessId != business.Id)
            return NotFound();

        item.ImageData = null;
        item.ImageContentType = null;
        await _context.SaveChangesAsync();

        return Ok(new { HasImage = false });
    }

    // ── Return items ─────────────────────────────────────────

    [HttpPut("{billId}/items/{itemId}/return")]
    public async Task<IActionResult> ReturnItems(Guid billId, Guid itemId, ReturnItemsRequest request)
    {
        if (request.QuantityToReturn <= 0)
            return BadRequest("Quantity to return must be at least 1");

        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var item = await _context.BillItems
            .Include(x => x.Bill)
            .FirstOrDefaultAsync(x => x.Id == itemId && x.BillId == billId);

        if (item == null || item.Bill?.BusinessId != business.Id)
            return NotFound();

        if (!item.IsRefundable)
            return BadRequest("This item is not marked as refundable");

        var maxReturnable = item.Quantity - item.QuantityReturned - item.QuantityBoughtByClient;
        if (request.QuantityToReturn > maxReturnable)
            return BadRequest($"Only {maxReturnable} unit(s) are available to return");

        item.QuantityReturned += request.QuantityToReturn;
        await _context.SaveChangesAsync();

        return Ok(ToItemResponse(item));
    }

    // ── Sell to client ───────────────────────────────────────

    [HttpPut("{billId}/items/{itemId}/sell")]
    public async Task<IActionResult> SellToClient(Guid billId, Guid itemId, SellToClientRequest request)
    {
        if (request.QuantityToSell <= 0)
            return BadRequest("Quantity must be at least 1");

        if (string.IsNullOrWhiteSpace(request.ClientName))
            return BadRequest("Client name is required");

        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var item = await _context.BillItems
            .Include(x => x.Bill)
            .FirstOrDefaultAsync(x => x.Id == itemId && x.BillId == billId);

        if (item == null || item.Bill?.BusinessId != business.Id)
            return NotFound();

        var maxSellable = item.Quantity - item.QuantityReturned - item.QuantityBoughtByClient;
        if (request.QuantityToSell > maxSellable)
            return BadRequest($"Only {maxSellable} unit(s) are available to sell");

        item.QuantityBoughtByClient += request.QuantityToSell;
        item.BoughtByClientName = request.ClientName.Trim();
        item.BoughtByClientId = request.ClientId;

        // Create or update draft invoice for this client + project
        var draftInvoiceId = await UpsertDraftInvoiceAsync(business.Id, item, request);
        item.DraftInvoiceId = draftInvoiceId;

        await _context.SaveChangesAsync();

        return Ok(ToItemResponse(item));
    }

    // ── Projects dropdown ─────────────────────────────────────

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects()
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        // Pull project names from all sources: Projects table, invoice items, calendar events, bills.
        var fromProjects = await _context.Projects
            .Where(p => p.BusinessId == business.Id && p.Status != Domain.Entities.ProjectStatus.Archived)
            .Select(p => new { Name = p.Name, Date = p.CreatedAt })
            .ToListAsync();

        var fromItems = await (
            from item in _context.InvoiceItems
            join invoice in _context.Invoices on item.InvoiceId equals invoice.Id
            where invoice.BusinessId == business.Id && item.ProjectName != null && item.ProjectName != ""
            select new { Name = item.ProjectName!, Date = item.ItemDate }
        ).ToListAsync();

        var fromEvents = await _context.CalendarEvents
            .Where(e => e.BusinessId == business.Id)
            .Select(e => new { Name = e.Title, Date = e.EventDate })
            .ToListAsync();

        var fromBills = await _context.Bills
            .Where(b => b.BusinessId == business.Id)
            .Select(b => new { Name = b.ProjectName, Date = b.BillDate })
            .ToListAsync();

        var names = fromProjects
            .Concat(fromItems)
            .Concat(fromEvents)
            .Concat(fromBills)
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Name = g.First().Name.Trim(), LastUsed = g.Max(x => x.Date) })
            .OrderByDescending(x => x.LastUsed)
            .Select(x => x.Name)
            .ToList();

        return Ok(names);
    }

    // ════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════════════

    private async Task<Guid> UpsertDraftInvoiceAsync(
        Guid businessId, BillItem item, SellToClientRequest request)
    {
        var bill = item.Bill!;

        // Find an existing Draft invoice for this client + project
        Guid? clientId = request.ClientId;

        // Resolve client if only name given
        if (!clientId.HasValue && !string.IsNullOrWhiteSpace(request.ClientName))
        {
            var existing = await _context.Clients
                .FirstOrDefaultAsync(c => c.BusinessId == businessId
                    && c.ClientName == request.ClientName.Trim());
            clientId = existing?.Id;
        }

        Invoice? draft = null;

        if (item.DraftInvoiceId.HasValue)
        {
            draft = await _context.Invoices
                .FirstOrDefaultAsync(i => i.Id == item.DraftInvoiceId.Value
                    && i.BusinessId == businessId
                    && i.PaymentStatus == PaymentStatus.Draft);
        }

        if (draft == null && clientId.HasValue)
        {
            // Look for an existing draft for same client
            draft = await (
                from inv in _context.Invoices
                join ii in _context.InvoiceItems on inv.Id equals ii.InvoiceId
                where inv.BusinessId == businessId
                   && inv.PaymentStatus == PaymentStatus.Draft
                   && inv.ClientId == clientId.Value
                   && ii.ProjectName == bill.ProjectName
                select inv
            ).FirstOrDefaultAsync();
        }

        if (draft == null)
        {
            // Create a new Draft invoice
            var client = clientId.HasValue
                ? await _context.Clients.FindAsync(clientId.Value)
                : null;

            // Resolve ProjectId from the bill's linked project
            Guid? draftProjectId = bill.ProjectId;
            if (!draftProjectId.HasValue && !string.IsNullOrWhiteSpace(bill.ProjectName))
            {
                var proj = await _context.Projects
                    .FirstOrDefaultAsync(p => p.BusinessId == businessId
                        && p.Name == bill.ProjectName.Trim());
                draftProjectId = proj?.Id;
            }

            draft = new Invoice
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                ClientId = clientId ?? Guid.Empty,
                ProjectId = draftProjectId,
                InvoiceNumber = $"DRAFT-{DateTime.UtcNow.Ticks}",
                InvoiceDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(30),
                PaymentStatus = PaymentStatus.Draft,
                InvoiceStatus = "Draft",
                SubTotal = 0,
                TotalAmount = 0,
                RemainingAmount = 0,
                Notes = $"Draft created from bill: {bill.BrandName} / {bill.ProjectName}"
            };

            // Copy client name to invoice ClientName lookup if client exists
            if (client != null)
            {
                draft.Notes = $"Client: {client.ClientName} | {draft.Notes}";
            }

            _context.Invoices.Add(draft);
        }

        // Add a line item for the sold quantity
        var lineItem = new InvoiceItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = draft.Id,
            ExpenseName = item.ItemName,
            ProjectName = bill.ProjectName,
            ItemDate = DateTime.UtcNow,
            Amount = item.PricePerItem,
            Quantity = request.QuantityToSell,
            Total = item.PricePerItem * request.QuantityToSell
        };

        _context.InvoiceItems.Add(lineItem);
        await _context.SaveChangesAsync();

        // Recalculate draft totals from DB after save (avoids double-count)
        var allItems = await _context.InvoiceItems
            .Where(x => x.InvoiceId == draft.Id)
            .ToListAsync();

        var subtotal = allItems.Sum(x => x.Total);
        draft.SubTotal = subtotal;
        draft.TotalAmount = subtotal;
        draft.RemainingAmount = subtotal;

        await _context.SaveChangesAsync();

        return draft.Id;
    }

    private static string? ValidateBill(CreateBillRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.ProjectName)) return "Project is required";
        if (string.IsNullOrWhiteSpace(r.BrandName)) return "Brand name is required";
        if (string.IsNullOrWhiteSpace(r.PaidWith)) return "Payment method is required";
        if (!AllowedPaymentMethods.Contains(r.PaidWith.Trim(), StringComparer.OrdinalIgnoreCase))
            return $"Payment method must be one of: {string.Join(", ", AllowedPaymentMethods)}";
        if (!r.Items.Any()) return "Add at least one item";
        foreach (var item in r.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ItemName)) return "Each item needs a name";
            if (item.Quantity < 1) return "Quantity must be at least 1";
            if (item.PricePerItem < 0) return "Price cannot be negative";
        }
        return null;
    }

    private static BillResponse ToResponse(Bill b) => new()
    {
        Id = b.Id,
        ProjectName = b.ProjectName,
        BrandName = b.BrandName,
        BillDate = b.BillDate,
        PaidWith = b.PaidWith,
        Notes = b.Notes,
        Items = b.Items.Select(ToItemResponse).ToList(),
        TotalCost = b.Items.Sum(i => i.TotalCost),
        TotalRefunded = b.Items.Sum(i => i.AmountRefunded),
        TotalBought = b.Items.Sum(i => i.AmountBoughtByClient),
        TotalPending = b.Items.Sum(i => i.QuantityPending * i.PricePerItem)
    };

    private static BillItemResponse ToItemResponse(BillItem i) => new()
    {
        Id = i.Id,
        ItemName = i.ItemName,
        Quantity = i.Quantity,
        PricePerItem = i.PricePerItem,
        TotalCost = i.TotalCost,
        IsRefundable = i.IsRefundable,
        ReturnByDate = i.ReturnByDate,
        QuantityReturned = i.QuantityReturned,
        AmountRefunded = i.AmountRefunded,
        QuantityBoughtByClient = i.QuantityBoughtByClient,
        AmountBoughtByClient = i.AmountBoughtByClient,
        BoughtByClientName = i.BoughtByClientName,
        BoughtByClientId = i.BoughtByClientId,
        DraftInvoiceId = i.DraftInvoiceId,
        QuantityPending = i.QuantityPending,
        HasImage = i.ImageData != null && i.ImageData.Length > 0,
        Notes = i.Notes
    };

    private async Task<Business?> GetBusinessAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim)) return null;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return null;
        return await _context.Businesses.FirstOrDefaultAsync(x => x.UserId == userId);
    }
}
