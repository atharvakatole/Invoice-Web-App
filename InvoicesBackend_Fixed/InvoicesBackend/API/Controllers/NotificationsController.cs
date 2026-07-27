using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicesBackend.Persistence;

namespace InvoicesBackend.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public NotificationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] bool unreadOnly = false)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var query = _context.Notifications
            .Where(n => n.BusinessId == business.Id);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        var list = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.Title,
                n.Message,
                n.IsRead,
                n.LinkPath,
                n.RelatedEntityId,
                n.CreatedAt
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var count = await _context.Notifications
            .CountAsync(n => n.BusinessId == business.Id && !n.IsRead);

        return Ok(new { count });
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var n = await _context.Notifications
            .FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == business.Id);

        if (n == null) return NotFound();

        n.IsRead = true;
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Marked as read" });
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        await _context.Notifications
            .Where(n => n.BusinessId == business.Id && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        return Ok(new { Message = "All marked as read" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var n = await _context.Notifications
            .FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == business.Id);

        if (n == null) return NotFound();

        _context.Notifications.Remove(n);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Notification removed" });
    }

    [HttpDelete("clear-all")]
    public async Task<IActionResult> ClearAll()
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        await _context.Notifications
            .Where(n => n.BusinessId == business.Id)
            .ExecuteDeleteAsync();

        return Ok(new { Message = "All notifications cleared" });
    }

    private async Task<Domain.Entities.Business?> GetBusinessAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim)) return null;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return null;
        return await _context.Businesses.FirstOrDefaultAsync(x => x.UserId == userId);
    }
}
