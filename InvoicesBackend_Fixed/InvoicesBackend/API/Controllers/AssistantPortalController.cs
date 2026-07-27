using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicesBackend.Application.DTOs;
using InvoicesBackend.Domain.Entities;
using InvoicesBackend.Domain.Enums;
using InvoicesBackend.Persistence;
using InvoicesBackend.Services;

namespace InvoicesBackend.API.Controllers;

/// <summary>
/// All endpoints used by the assistant-mode frontend.
/// Requires role = AssistantUser.
/// </summary>
[ApiController]
[Route("api/assistant")]
[Authorize] // Any authenticated user can access assistant portal; scoped by their assistant records
public class AssistantPortalController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly JwtService _jwtService;
    private readonly EmailService _emailService;

    public AssistantPortalController(
        ApplicationDbContext context,
        JwtService jwtService,
        EmailService emailService)
    {
        _context = context;
        _jwtService = jwtService;
        _emailService = emailService;
    }

    // ── Profile & mode ────────────────────────────────────

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        var assistants = await _context.Assistants
            .Where(a => a.UserId == userId.Value)
            .ToListAsync();

        return Ok(new
        {
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            Assistants = assistants.Select(a => new { a.Id, a.Name, a.BusinessId })
        });
    }

    // ── Assignments ────────────────────────────────────────

    [HttpGet("assignments")]
    public async Task<IActionResult> GetAssignments([FromQuery] string? status)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var assistantIds = await _context.Assistants
            .Where(a => a.UserId == userId.Value)
            .Select(a => a.Id)
            .ToListAsync();

        var query = _context.AssistantAssignments
            .Include(a => a.Project)
            .Where(a => assistantIds.Contains(a.AssistantId));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AssignmentStatus>(status, true, out var s))
            query = query.Where(a => a.Status == s);

        var assignments = await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        // Get business names
        var businessIds = assignments.Select(a => a.BusinessId).Distinct().ToList();
        var businesses = await _context.Businesses
            .Where(b => businessIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.BusinessName ?? string.Empty);

        return Ok(assignments.Select(a => new
        {
            a.Id,
            a.BusinessId,
            BusinessName = businesses.TryGetValue(a.BusinessId, out var bn) ? bn : string.Empty,
            a.ProjectId,
            ProjectName = a.Project?.Name ?? a.ProjectName,
            a.WorkDates,
            a.Fee,
            a.IsPaid,
            a.Notes,
            Status = a.Status.ToString(),
            a.CreatedAt
        }));
    }

    [HttpPut("assignments/{id}/respond")]
    public async Task<IActionResult> RespondToAssignment(Guid id, RespondToAssignmentRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var assistantIds = await _context.Assistants
            .Where(a => a.UserId == userId.Value)
            .Select(a => a.Id)
            .ToListAsync();

        var assignment = await _context.AssistantAssignments
            .Include(a => a.Project)
            .FirstOrDefaultAsync(a => a.Id == id && assistantIds.Contains(a.AssistantId));

        if (assignment == null) return NotFound();

        if (assignment.Status != AssignmentStatus.Pending)
            return BadRequest($"Assignment is already {assignment.Status}");

        var isAccept = request.Response.Equals("accept", StringComparison.OrdinalIgnoreCase);
        assignment.Status = isAccept ? AssignmentStatus.Accepted : AssignmentStatus.Rejected;

        // Notify manager
        var assistant = await _context.Assistants.FindAsync(assistantIds.First());
        var projectLabel = assignment.Project?.Name ?? assignment.ProjectName;
        var notifMsg = isAccept
            ? $"{assistant?.Name ?? "Your assistant"} accepted the assignment for \"{projectLabel}\"."
            : $"{assistant?.Name ?? "Your assistant"} declined the assignment for \"{projectLabel}\". Reason: {request.Reason ?? "Not specified"}";

        _context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            BusinessId = assignment.BusinessId,
            Type = isAccept ? "assignment_accepted" : "assignment_rejected",
            Title = isAccept ? "Assignment Accepted" : "Assignment Declined",
            Message = notifMsg,
            LinkPath = "/app/assistants"
        });

        await _context.SaveChangesAsync();

        return Ok(new { Status = assignment.Status.ToString() });
    }

    // ── Bills (scoped to assigned projects) ───────────────

    [HttpGet("bills")]
    public async Task<IActionResult> GetBills()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var assistantIds = await _context.Assistants
            .Where(a => a.UserId == userId.Value)
            .Select(a => a.Id)
            .ToListAsync();

        // Get accepted assignments (both by ProjectId and ProjectName for fallback)
        var assignments = await _context.AssistantAssignments
            .Where(a => assistantIds.Contains(a.AssistantId)
                && (a.Status == AssignmentStatus.Accepted || a.Status == AssignmentStatus.Pending))
            .ToListAsync();

        var projectIds = assignments
            .Where(a => a.ProjectId != null)
            .Select(a => a.ProjectId!.Value)
            .Distinct()
            .ToList();

        var projectNames = assignments
            .Where(a => !string.IsNullOrWhiteSpace(a.ProjectName))
            .Select(a => a.ProjectName.Trim().ToLower())
            .Distinct()
            .ToList();

        // Collect all business IDs from assignments (assistant may work for multiple businesses)
        var businessIds = assignments.Select(a => a.BusinessId).Distinct().ToList();

        // Match bills by ProjectId OR by ProjectName (for bills without ProjectId set)
        // Scoped to the correct businesses for security
        var bills = await _context.Bills
            .Include(b => b.Items)
            .Where(b => businessIds.Contains(b.BusinessId)
                && (
                    (b.ProjectId != null && projectIds.Contains(b.ProjectId.Value))
                    || projectNames.Contains(b.ProjectName.Trim().ToLower())
                ))
            .OrderByDescending(b => b.BillDate)
            .ToListAsync();

        return Ok(bills.Select(b => new
        {
            b.Id,
            b.ProjectName,
            b.BrandName,
            b.BillDate,
            b.PaidWith,
            b.Notes,
            Items = b.Items.Select(i => new
            {
                i.Id,
                i.ItemName,
                i.Quantity,
                i.PricePerItem,
                i.TotalCost,
                i.IsRefundable,
                i.ReturnByDate,
                i.QuantityReturned,
                i.QuantityBoughtByClient,
                i.QuantityPending,
                i.BoughtByClientName,
                HasImage = i.ImageData != null && i.ImageData.Length > 0,
                i.Notes
            }),
            TotalCost = b.Items.Sum(i => i.TotalCost)
        }));
    }

    [HttpPost("bills")]
    public async Task<IActionResult> AddBill(CreateAssistantBillRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var assistant = await _context.Assistants
            .FirstOrDefaultAsync(a => a.UserId == userId.Value);
        if (assistant == null) return BadRequest("Assistant profile not found");

        // Verify assignment exists and is accepted for this project
        var assignment = await _context.AssistantAssignments
            .FirstOrDefaultAsync(a => a.AssistantId == assistant.Id
                && a.ProjectId == request.ProjectId
                && (a.Status == AssignmentStatus.Accepted || a.Status == AssignmentStatus.Pending));

        if (assignment == null)
            return BadRequest("You are not assigned to this project or assignment is not accepted");

        var bill = new Bill
        {
            Id = Guid.NewGuid(),
            BusinessId = assistant.BusinessId,
            ProjectId = request.ProjectId,
            ProjectName = request.ProjectName,
            BrandName = request.BrandName,
            BillDate = DateTime.SpecifyKind(request.BillDate.Date, DateTimeKind.Utc),
            PaidWith = request.PaidWith,
            Notes = $"[Added by assistant: {assistant.Name}] {request.Notes}".Trim(),
        };

        foreach (var item in request.Items)
        {
            bill.Items.Add(new BillItem
            {
                Id = Guid.NewGuid(),
                BillId = bill.Id,
                ItemName = item.ItemName,
                Quantity = item.Quantity,
                PricePerItem = item.PricePerItem,
                IsRefundable = item.IsRefundable,
                ReturnByDate = item.ReturnByDate.HasValue
                    ? DateTime.SpecifyKind(item.ReturnByDate.Value.Date, DateTimeKind.Utc)
                    : null
            });
        }

        _context.Bills.Add(bill);

        // Notify manager that assistant added a bill
        _context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            BusinessId = assistant.BusinessId,
            Type = "assistant_bill_added",
            Title = "Bill Added by Assistant",
            Message = $"{assistant.Name} added a bill from {request.BrandName} for project \"{request.ProjectName}\".",
            LinkPath = "/app/bills"
        });

        await _context.SaveChangesAsync();
        return Ok(new { bill.Id, Message = "Bill added successfully" });
    }

    // ── Return Requests ────────────────────────────────────

    [HttpPost("return-requests")]
    public async Task<IActionResult> SubmitReturnRequest(AssistantReturnRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        // Get ALL assistant records for this user (they may work for multiple businesses)
        var assistantIds = await _context.Assistants
            .Where(a => a.UserId == userId.Value)
            .Select(a => a.Id)
            .ToListAsync();

        if (!assistantIds.Any()) return BadRequest("Assistant profile not found");

        var item = await _context.BillItems
            .Include(i => i.Bill)
            .FirstOrDefaultAsync(i => i.Id == request.BillItemId);

        if (item == null) return NotFound("Bill item not found");
        if (!item.IsRefundable) return BadRequest("This item is not marked as refundable");

        // Verify assistant has access to this bill's business/project
        var hasAccess = await _context.AssistantAssignments
            .AnyAsync(a => assistantIds.Contains(a.AssistantId)
                && a.BusinessId == item.Bill!.BusinessId
                && (a.ProjectId == item.Bill.ProjectId
                    || (item.Bill.ProjectName != null && a.ProjectName == item.Bill.ProjectName)));

        if (!hasAccess) return Forbid();

        // Auto-resolve AssignmentId if not provided
        if (request.AssignmentId == Guid.Empty)
        {
            var resolvedAssignment = await _context.AssistantAssignments
                .FirstOrDefaultAsync(a => assistantIds.Contains(a.AssistantId)
                    && a.BusinessId == item.Bill!.BusinessId);
            if (resolvedAssignment != null)
                request.AssignmentId = resolvedAssignment.Id;
        }

        var assistant = await _context.Assistants
            .FirstOrDefaultAsync(a => assistantIds.Contains(a.Id));

        var available = item.QuantityPending;
        if (request.QuantityToReturn < 1 || request.QuantityToReturn > available)
            return BadRequest($"Only {available} unit(s) available to return");

        // Check no pending request already exists for this item
        var existingPending = await _context.ReturnRequests
            .AnyAsync(r => r.BillItemId == request.BillItemId
                && r.AssistantUserId == userId.Value
                && r.Status == ReturnRequestStatus.Pending);

        if (existingPending)
            return BadRequest("You already have a pending return request for this item");

        var returnReq = new ReturnRequest
        {
            Id = Guid.NewGuid(),
            BillItemId = request.BillItemId,
            AssignmentId = request.AssignmentId,
            AssistantUserId = userId.Value,
            QuantityToReturn = request.QuantityToReturn,
            Notes = request.Notes?.Trim(),
            Status = ReturnRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.ReturnRequests.Add(returnReq);

        // Notify manager
        _context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            BusinessId = item.Bill!.BusinessId,
            Type = "return_request_pending",
            Title = "🔄 Return Request",
            Message = $"{assistant.Name} has returned {request.QuantityToReturn}x \"{item.ItemName}\" from {item.Bill.BrandName}. Please verify and approve.",
            LinkPath = "/app/bills",
            RelatedEntityId = returnReq.Id
        });

        await _context.SaveChangesAsync();

        return Ok(new { returnReq.Id, Message = "Return request submitted. Awaiting manager approval." });
    }

    [HttpGet("return-requests")]
    public async Task<IActionResult> GetMyReturnRequests()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var requests = await _context.ReturnRequests
            .Include(r => r.BillItem).ThenInclude(bi => bi!.Bill)
            .Where(r => r.AssistantUserId == userId.Value)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(requests.Select(r => new
        {
            r.Id,
            r.QuantityToReturn,
            r.Notes,
            r.ManagerNotes,
            Status = r.Status.ToString(),
            r.CreatedAt,
            r.ResolvedAt,
            ItemName = r.BillItem?.ItemName,
            BrandName = r.BillItem?.Bill?.BrandName,
            ProjectName = r.BillItem?.Bill?.ProjectName
        }));
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

// DTO for assistant bill creation
public class CreateAssistantBillRequest
{
    public Guid? ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
    public string PaidWith { get; set; } = "Cash";
    public string? Notes { get; set; }
    public List<AssistantBillItemRequest> Items { get; set; } = new();
}

public class AssistantBillItemRequest
{
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal PricePerItem { get; set; }
    public bool IsRefundable { get; set; }
    public DateTime? ReturnByDate { get; set; }
}
