using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicesBackend.Application.DTOs;
using InvoicesBackend.Domain.Entities;
using InvoicesBackend.Persistence;

namespace InvoicesBackend.API.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public ProjectsController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetProjects([FromQuery] string? status, [FromQuery] Guid? clientId)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var query = _context.Projects
            .Include(p => p.Client)
            .Where(p => p.BusinessId == business.Id);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ProjectStatus>(status, true, out var s))
            query = query.Where(p => p.Status == s);

        if (clientId.HasValue)
            query = query.Where(p => p.ClientId == clientId.Value);

        var projects = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        var projectIds = projects.Select(p => p.Id).ToList();

        var invoiceCounts = await _context.Invoices
            .Where(i => i.ProjectId != null && projectIds.Contains(i.ProjectId!.Value))
            .GroupBy(i => i.ProjectId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count(), Total = g.Sum(i => i.TotalAmount) })
            .ToDictionaryAsync(x => x.Id);

        var assistantCounts = await _context.AssistantAssignments
            .Where(a => a.ProjectId != null && projectIds.Contains(a.ProjectId!.Value))
            .GroupBy(a => a.ProjectId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id);

        var billCounts = await _context.Bills
            .Where(b => b.ProjectId != null && projectIds.Contains(b.ProjectId!.Value))
            .GroupBy(b => b.ProjectId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id);

        return Ok(projects.Select(p => new ProjectResponse
        {
            Id = p.Id,
            ClientId = p.ClientId,
            ClientName = p.Client?.ClientName ?? string.Empty,
            Name = p.Name,
            Description = p.Description,
            Status = p.Status.ToString(),
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            Budget = p.Budget,
            Notes = p.Notes,
            InvoiceCount = invoiceCounts.TryGetValue(p.Id, out var ic) ? ic.Count : 0,
            TotalInvoiced = invoiceCounts.TryGetValue(p.Id, out var it) ? it.Total : 0,
            AssistantCount = assistantCounts.TryGetValue(p.Id, out var ac) ? ac.Count : 0,
            BillCount = billCounts.TryGetValue(p.Id, out var bc) ? bc.Count : 0,
            CreatedAt = p.CreatedAt
        }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProject(Guid id)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var project = await _context.Projects
            .Include(p => p.Client)
            .FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == business.Id);

        if (project == null) return NotFound();

        // Load all related data for the project detail page
        var invoices = await _context.Invoices
            .Where(i => i.ProjectId == id)
            .ToListAsync();

        var assignments = await _context.AssistantAssignments
            .Where(a => a.ProjectId == id)
            .Join(_context.Assistants, a => a.AssistantId, x => x.Id, (a, x) => new
            {
                a.Id, AssistantName = x.Name, a.WorkDates, a.Fee, a.IsPaid, a.Notes
            })
            .ToListAsync();

        var bills = await _context.Bills
            .Include(b => b.Items)
            .Where(b => b.ProjectId == id)
            .ToListAsync();

        var calEvents = await _context.CalendarEvents
            .Where(e => e.ProjectId == id)
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        return Ok(new
        {
            Project = new ProjectResponse
            {
                Id = project.Id,
                ClientId = project.ClientId,
                ClientName = project.Client?.ClientName ?? string.Empty,
                Name = project.Name,
                Description = project.Description,
                Status = project.Status.ToString(),
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                Budget = project.Budget,
                Notes = project.Notes,
                InvoiceCount = invoices.Count,
                TotalInvoiced = invoices.Sum(i => i.TotalAmount),
                AssistantCount = assignments.Count,
                BillCount = bills.Count,
                CreatedAt = project.CreatedAt
            },
            Invoices = invoices.Select(i => new { i.Id, i.InvoiceNumber, i.InvoiceDate, i.TotalAmount, i.PaymentStatus }),
            Assignments = assignments,
            Bills = bills.Select(b => new
            {
                b.Id, b.BrandName, b.BillDate, b.PaidWith,
                TotalCost = b.Items.Sum(i => i.TotalCost),
                Items = b.Items.Select(i => new { i.Id, i.ItemName, i.Quantity, i.PricePerItem, i.QuantityPending, i.QuantityBoughtByClient })
            }),
            CalendarEvents = calEvents.Select(e => new { e.Id, e.Title, e.EventDate, e.Notes })
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Project name is required");

        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.Id == request.ClientId && c.BusinessId == business.Id);
        if (client == null) return BadRequest("Client not found");

        var project = new Project
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            ClientId = request.ClientId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            StartDate = request.StartDate.HasValue ? DateTime.SpecifyKind(request.StartDate.Value, DateTimeKind.Utc) : null,
            EndDate = request.EndDate.HasValue ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc) : null,
            Budget = request.Budget,
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        return Ok(new ProjectResponse
        {
            Id = project.Id, ClientId = project.ClientId, ClientName = client.ClientName ?? string.Empty,
            Name = project.Name, Description = project.Description, Status = project.Status.ToString(),
            StartDate = project.StartDate, EndDate = project.EndDate, Budget = project.Budget,
            Notes = project.Notes, CreatedAt = project.CreatedAt
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Project name is required");

        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var project = await _context.Projects
            .Include(p => p.Client)
            .FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == business.Id);
        if (project == null) return NotFound();

        project.Name = request.Name.Trim();
        project.Description = request.Description?.Trim();
        project.StartDate = request.StartDate.HasValue ? DateTime.SpecifyKind(request.StartDate.Value, DateTimeKind.Utc) : null;
        project.EndDate = request.EndDate.HasValue ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc) : null;
        project.Budget = request.Budget;
        project.Notes = request.Notes?.Trim();
        project.Status = Enum.TryParse<ProjectStatus>(request.Status, true, out var s) ? s : ProjectStatus.Active;
        project.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new ProjectResponse
        {
            Id = project.Id, ClientId = project.ClientId, ClientName = project.Client?.ClientName ?? string.Empty,
            Name = project.Name, Description = project.Description, Status = project.Status.ToString(),
            StartDate = project.StartDate, EndDate = project.EndDate, Budget = project.Budget,
            Notes = project.Notes, CreatedAt = project.CreatedAt
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == business.Id);
        if (project == null) return NotFound();

        // Don't delete if it has real (non-draft) invoices
        var hasInvoices = await _context.Invoices
            .AnyAsync(i => i.ProjectId == id && i.PaymentStatus != Domain.Enums.PaymentStatus.Draft);
        if (hasInvoices)
            return BadRequest("Cannot delete a project that has active invoices. Archive it instead.");

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Project removed" });
    }

    private async Task<Business?> GetBusinessAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim)) return null;
        var userId = Guid.Parse(userIdClaim);
        return await _context.Businesses.FirstOrDefaultAsync(x => x.UserId == userId);
    }
}
