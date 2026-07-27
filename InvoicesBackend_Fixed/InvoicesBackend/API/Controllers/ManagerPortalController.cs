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
/// Manager endpoints: invite assistants, see return requests, approve/reject returns.
/// </summary>
[ApiController]
[Route("api/manager")]
[Authorize]
public class ManagerPortalController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly JwtService _jwtService;
    private readonly EmailService _emailService;

    public ManagerPortalController(
        ApplicationDbContext context,
        JwtService jwtService,
        EmailService emailService)
    {
        _context = context;
        _jwtService = jwtService;
        _emailService = emailService;
    }

    // ── Invite assistant ───────────────────────────────────

    [HttpPost("invite-assistant")]
    public async Task<IActionResult> InviteAssistant(InviteAssistantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Name is required");
        if (string.IsNullOrWhiteSpace(request.Email)) return BadRequest("Email is required");

        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var email = request.Email.Trim().ToLowerInvariant();

        // Find or create User account for the assistant
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        bool isNewUser = user == null;

        if (user == null)
        {
            var phone = request.Phone?.Trim() ?? string.Empty;
            user = new User
            {
                Id = Guid.NewGuid(),
                FullName = request.Name.Trim(),
                Username = await GenerateUsernameAsync(email),
                Email = email,
                PhoneNumber = phone,
                // Default password = phone number (or "changeme" if no phone)
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                    string.IsNullOrWhiteSpace(phone) ? "changeme123" : phone),
                Role = UserRole.AssistantUser
            };
            _context.Users.Add(user);
        }
        else if (user.Role == UserRole.BusinessOwner)
        {
            // This person is already a manager — they can also be an assistant
            // Don't downgrade their role, just link the assistant record
        }

        // Create or find the Assistant record for this business
        var assistant = await _context.Assistants
            .FirstOrDefaultAsync(a => a.BusinessId == business.Id
                && (a.Email == email || a.UserId == user.Id));

        if (assistant == null)
        {
            assistant = new Assistant
            {
                Id = Guid.NewGuid(),
                BusinessId = business.Id,
                UserId = user.Id,
                Name = request.Name.Trim(),
                Phone = request.Phone?.Trim(),
                Email = email
            };
            _context.Assistants.Add(assistant);
        }
        else
        {
            // Update link
            assistant.UserId = user.Id;
            assistant.Email = email;
        }

        await _context.SaveChangesAsync();

        // Send welcome email
        if (isNewUser)
        {
            try
            {
                var loginPassword = string.IsNullOrWhiteSpace(request.Phone) ? "changeme123" : request.Phone.Trim();
                await _emailService.SendAssistantInviteAsync(
                    email, request.Name.Trim(), business.BusinessName ?? "your manager", loginPassword);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send assistant invite email to {Email}", email);
                // Don't fail the request — assistant account was created
            }
        }

        return Ok(new
        {
            AssistantId = assistant.Id,
            UserId = user.Id,
            IsNewAccount = isNewUser,
            Message = isNewUser
                ? $"Invitation sent to {email}. Default password is their phone number."
                : $"Existing user linked as assistant for your business."
        });
    }

    // ── Return requests ────────────────────────────────────

    [HttpGet("return-requests")]
    public async Task<IActionResult> GetReturnRequests([FromQuery] string? status)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var query = _context.ReturnRequests
            .Include(r => r.BillItem).ThenInclude(bi => bi!.Bill)
            .Where(r => r.BillItem!.Bill!.BusinessId == business.Id);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReturnRequestStatus>(status, true, out var s))
            query = query.Where(r => r.Status == s);

        var requests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

        // Get assistant names
        var userIds = requests.Select(r => r.AssistantUserId).Distinct().ToList();
        var assistantNames = await _context.Assistants
            .Where(a => a.UserId != null && userIds.Contains(a.UserId.Value) && a.BusinessId == business.Id)
            .ToDictionaryAsync(a => a.UserId!.Value, a => a.Name);

        return Ok(requests.Select(r => new
        {
            r.Id,
            r.QuantityToReturn,
            r.Notes,
            r.ManagerNotes,
            Status = r.Status.ToString(),
            r.CreatedAt,
            r.ResolvedAt,
            AssistantName = assistantNames.TryGetValue(r.AssistantUserId, out var n) ? n : "Unknown",
            ItemName = r.BillItem?.ItemName,
            BrandName = r.BillItem?.Bill?.BrandName,
            ProjectName = r.BillItem?.Bill?.ProjectName,
            BillItemId = r.BillItemId,
            PricePerItem = r.BillItem?.PricePerItem ?? 0,
            EstimatedRefund = (r.BillItem?.PricePerItem ?? 0) * r.QuantityToReturn
        }));
    }

    [HttpPut("return-requests/{id}/resolve")]
    public async Task<IActionResult> ResolveReturnRequest(Guid id, ResolveReturnRequest request)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var returnReq = await _context.ReturnRequests
            .Include(r => r.BillItem).ThenInclude(bi => bi!.Bill)
            .FirstOrDefaultAsync(r => r.Id == id
                && r.BillItem!.Bill!.BusinessId == business.Id);

        if (returnReq == null) return NotFound();
        if (returnReq.Status != ReturnRequestStatus.Pending)
            return BadRequest($"This return request is already {returnReq.Status}");

        var isApproved = request.Resolution.Equals("approve", StringComparison.OrdinalIgnoreCase);
        returnReq.Status = isApproved ? ReturnRequestStatus.Approved : ReturnRequestStatus.Rejected;
        returnReq.ManagerNotes = request.ManagerNotes?.Trim();
        returnReq.ResolvedAt = DateTime.UtcNow;

        if (isApproved)
        {
            // Actually mark the items as returned
            returnReq.BillItem!.QuantityReturned += returnReq.QuantityToReturn;
        }

        // Notify the assistant
        var assistantUser = await _context.Users.FindAsync(returnReq.AssistantUserId);
        if (assistantUser != null)
        {
            // Find which business this user is an assistant in to get their assistantId
            var assistantRecord = await _context.Assistants
                .FirstOrDefaultAsync(a => a.UserId == returnReq.AssistantUserId
                    && a.BusinessId == business.Id);

            var itemName = returnReq.BillItem?.ItemName ?? "item";
            var notifMsg = isApproved
                ? $"Your return of {returnReq.QuantityToReturn}x \"{itemName}\" has been approved by the manager."
                : $"Your return request for \"{itemName}\" was rejected. Manager note: {request.ManagerNotes ?? "None"}";

            // We notify the assistant via the shared notification system
            // (assistant can see notifications in their portal)
            _context.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                BusinessId = business.Id,
                Type = isApproved ? "return_approved" : "return_rejected",
                Title = isApproved ? "✅ Return Approved" : "❌ Return Rejected",
                Message = notifMsg,
                LinkPath = "/assistant/returns",
                RelatedEntityId = returnReq.Id
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            Status = returnReq.Status.ToString(),
            Message = isApproved ? "Return approved and item marked as returned" : "Return request rejected"
        });
    }

    [HttpGet("pending-return-count")]
    public async Task<IActionResult> GetPendingReturnCount()
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var count = await _context.ReturnRequests
            .Include(r => r.BillItem).ThenInclude(bi => bi!.Bill)
            .CountAsync(r => r.BillItem!.Bill!.BusinessId == business.Id
                && r.Status == ReturnRequestStatus.Pending);

        return Ok(new { count });
    }

    private async Task<Business?> GetBusinessAsync()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var userId)) return null;
        return await _context.Businesses.FirstOrDefaultAsync(b => b.UserId == userId);
    }

    private async Task<string> GenerateUsernameAsync(string email)
    {
        var base_ = email.Split('@')[0];
        var candidate = base_;
        var suffix = 0;
        while (await _context.Users.AnyAsync(u => u.Username == candidate))
            candidate = $"{base_}{++suffix}";
        return candidate;
    }

    private Microsoft.Extensions.Logging.ILogger<ManagerPortalController> _logger =>
        HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ManagerPortalController>>();
}
