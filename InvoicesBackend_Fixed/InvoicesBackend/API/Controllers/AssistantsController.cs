using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using InvoicesBackend.Services;
using InvoicesBackend.Domain.Enums;
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
    private readonly PlanGuardService _planGuard;

    public AssistantsController(ApplicationDbContext context, PlanGuardService planGuard)
    {
        _context = context;
        _planGuard = planGuard;
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

        var premUid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(premUid, out var premUidG))
        {
            var premErr = await _planGuard.RequirePremiumAsync(premUidG, "Assistants");
            if (premErr != null) return BadRequest(premErr);
        }


        Guid? linkedUserId = null;
        bool isNewUser = false;

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (existingUser != null)
            {
                linkedUserId = existingUser.Id;
            }
            else
            {
                // Create a new AssistantUser account
                var phone = request.Phone?.Trim() ?? string.Empty;
                var defaultPassword = string.IsNullOrWhiteSpace(phone) ? "changeme123" : phone;

                var newUser = new User
                {
                    Id = Guid.NewGuid(),
                    FullName = request.Name.Trim(),
                    Username = await GenerateUsernameAsync(email),
                    Email = email,
                    PhoneNumber = phone,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                    Role = UserRole.AssistantUser
                };
                _context.Users.Add(newUser);
                linkedUserId = newUser.Id;
                isNewUser = true;

                // Send welcome email (fire and forget)
                try
                {
                    var emailService = HttpContext.RequestServices.GetRequiredService<EmailService>();
                    await emailService.SendAssistantInviteAsync(email, request.Name.Trim(), business.BusinessName ?? "your manager", defaultPassword);
                }
                catch { /* Don't fail the request if email fails */ }
            }
        }

        var assistant = new Assistant
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            Name = request.Name.Trim(),
            Phone = request.Phone,
            Email = request.Email?.Trim().ToLowerInvariant(),
            UserId = linkedUserId
        };

        _context.Assistants.Add(assistant);
        await _context.SaveChangesAsync();

        return Ok(new AssistantResponse
        {
            Id = assistant.Id,
            Name = assistant.Name,
            Phone = assistant.Phone,
            Email = assistant.Email,
            IsNewAccount = isNewUser
        });
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
        // ProjectName can be derived from ProjectId — only require it if ProjectId also absent
        if (string.IsNullOrWhiteSpace(request.ProjectName) && !request.ProjectId.HasValue)
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
            Guid? newLinkedUserId = null;

            if (!string.IsNullOrWhiteSpace(request.NewAssistantEmail))
            {
                var email = request.NewAssistantEmail.Trim().ToLowerInvariant();
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (existingUser != null)
                {
                    newLinkedUserId = existingUser.Id;
                }
                else
                {
                    var phone = request.NewAssistantPhone?.Trim() ?? string.Empty;
                    var defaultPw = string.IsNullOrWhiteSpace(phone) ? "changeme123" : phone;
                    var newUser = new User
                    {
                        Id = Guid.NewGuid(),
                        FullName = request.NewAssistantName.Trim(),
                        Username = await GenerateUsernameAsync(email),
                        Email = email,
                        PhoneNumber = phone,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPw),
                        Role = UserRole.AssistantUser
                    };
                    _context.Users.Add(newUser);
                    newLinkedUserId = newUser.Id;

                    try
                    {
                        var emailService = HttpContext.RequestServices.GetRequiredService<EmailService>();
                        await emailService.SendAssistantInviteAsync(email, request.NewAssistantName.Trim(), business.BusinessName ?? "your manager", defaultPw);
                    }
                    catch { /* Don't fail if email send fails */ }
                }
            }

            assistant = new Assistant
            {
                Id = Guid.NewGuid(),
                BusinessId = business.Id,
                Name = request.NewAssistantName.Trim(),
                Phone = request.NewAssistantPhone,
                Email = request.NewAssistantEmail?.Trim().ToLowerInvariant(),
                UserId = newLinkedUserId
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

        if (!Guid.TryParse(userIdClaim, out var userId))
            return null;

        return await _context.Businesses
            .FirstOrDefaultAsync(x => x.UserId == userId);
    }
}
