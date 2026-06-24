using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicesBackend.Application.DTOs;
using InvoicesBackend.Domain.Entities;
using InvoicesBackend.Persistence;

namespace InvoicesBackend.API.Controllers;

/// <summary>
/// Tracks freelance assistants/helpers a business works with: which
/// projects they helped on, which days they were hired, their fee, and
/// whether they've been paid.
/// </summary>
[ApiController]
[Route("api/assistants")]
[Authorize]
public class AssistantsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AssistantsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAssistants()
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var assistants = await _context.Assistants
            .Where(a => a.BusinessId == business.Id)
            .ToListAsync();

        var assignments = await _context.AssistantAssignments
            .Where(a => a.BusinessId == business.Id)
            .ToListAsync();

        var result = assistants.Select(a => new AssistantResponse
        {
            Id = a.Id,
            Name = a.Name,
            Phone = a.Phone,
            TotalAssignments = assignments.Count(x => x.AssistantId == a.Id),
            TotalUnpaid = assignments.Where(x => x.AssistantId == a.Id && !x.IsPaid).Sum(x => x.Fee)
        }).OrderBy(a => a.Name).ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAssistant(CreateAssistantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Assistant name is required");

        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var assistant = new Assistant
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            Name = request.Name.Trim(),
            Phone = request.Phone
        };

        _context.Assistants.Add(assistant);
        await _context.SaveChangesAsync();

        return Ok(new AssistantResponse { Id = assistant.Id, Name = assistant.Name, Phone = assistant.Phone });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAssistant(Guid id)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var assistant = await _context.Assistants
            .FirstOrDefaultAsync(a => a.Id == id && a.BusinessId == business.Id);

        if (assistant == null) return NotFound();

        var hasAssignments = await _context.AssistantAssignments
            .AnyAsync(a => a.AssistantId == id);

        if (hasAssignments)
            return BadRequest("Cannot delete an assistant with existing project assignments. Delete those first.");

        _context.Assistants.Remove(assistant);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Assistant removed" });
    }

    // ===================== Assignments =====================

    [HttpGet("assignments")]
    public async Task<IActionResult> GetAssignments()
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var assignments = await _context.AssistantAssignments
            .Where(a => a.BusinessId == business.Id)
            .Join(_context.Assistants, a => a.AssistantId, x => x.Id, (a, x) => new AssignmentResponse
            {
                Id = a.Id,
                AssistantId = a.AssistantId,
                AssistantName = x.Name,
                ProjectName = a.ProjectName,
                WorkDates = a.WorkDates,
                Fee = a.Fee,
                IsPaid = a.IsPaid,
                Notes = a.Notes
            })
            .ToListAsync();

        return Ok(assignments.OrderByDescending(a => a.WorkDates.Count > 0 ? a.WorkDates.Max() : DateTime.MinValue));
    }

    [HttpPost("assignments")]
    public async Task<IActionResult> CreateAssignment(CreateAssignmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectName))
            return BadRequest("Project name is required");

        if (request.WorkDates == null || request.WorkDates.Count == 0)
            return BadRequest("Select at least one work date");

        if (request.Fee < 0)
            return BadRequest("Fee cannot be negative");

        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        Assistant? assistant = null;

        if (request.AssistantId.HasValue)
        {
            assistant = await _context.Assistants
                .FirstOrDefaultAsync(a => a.Id == request.AssistantId.Value && a.BusinessId == business.Id);

            if (assistant == null)
                return BadRequest("Assistant not found");
        }
        else if (!string.IsNullOrWhiteSpace(request.NewAssistantName))
        {
            assistant = new Assistant
            {
                Id = Guid.NewGuid(),
                BusinessId = business.Id,
                Name = request.NewAssistantName.Trim(),
                Phone = request.NewAssistantPhone
            };
            _context.Assistants.Add(assistant);
        }
        else
        {
            return BadRequest("Provide an assistantId or a new assistant name");
        }

        // Resolve project name from ProjectId if provided
        string resolvedProjectName = request.ProjectName?.Trim() ?? string.Empty;
        if (request.ProjectId.HasValue && string.IsNullOrWhiteSpace(resolvedProjectName))
        {
            var proj = await _context.Projects.FindAsync(request.ProjectId.Value);
            if (proj != null) resolvedProjectName = proj.Name;
        }

        var assignment = new AssistantAssignment
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            AssistantId = assistant.Id,
            ProjectId = request.ProjectId,
            ProjectName = resolvedProjectName,
            WorkDates = request.WorkDates
                .Select(d => DateTime.SpecifyKind(d.Date, DateTimeKind.Utc))
                .Distinct()
                .OrderBy(d => d)
                .ToList(),
            Fee = request.Fee,
            IsPaid = request.IsPaid,
            Notes = request.Notes
        };

        _context.AssistantAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        return Ok(new AssignmentResponse
        {
            Id = assignment.Id,
            AssistantId = assistant.Id,
            AssistantName = assistant.Name,
            ProjectName = assignment.ProjectName,
            WorkDates = assignment.WorkDates,
            Fee = assignment.Fee,
            IsPaid = assignment.IsPaid,
            Notes = assignment.Notes
        });
    }

    [HttpPut("assignments/{id}/paid")]
    public async Task<IActionResult> SetPaid(Guid id, [FromBody] bool isPaid)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var assignment = await _context.AssistantAssignments
            .FirstOrDefaultAsync(a => a.Id == id && a.BusinessId == business.Id);

        if (assignment == null) return NotFound();

        assignment.IsPaid = isPaid;
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Updated" });
    }

    [HttpDelete("assignments/{id}")]
    public async Task<IActionResult> DeleteAssignment(Guid id)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var assignment = await _context.AssistantAssignments
            .FirstOrDefaultAsync(a => a.Id == id && a.BusinessId == business.Id);

        if (assignment == null) return NotFound();

        _context.AssistantAssignments.Remove(assignment);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Assignment removed" });
    }

    private async Task<Business?> GetBusinessAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return null;

        var userId = Guid.Parse(userIdClaim);

        return await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);
    }
}
