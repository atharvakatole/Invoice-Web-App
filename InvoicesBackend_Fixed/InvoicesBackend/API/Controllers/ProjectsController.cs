using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicesBackend.Application.DTOs;
using InvoicesBackend.Domain.Entities;
using InvoicesBackend.Persistence;
using InvoicesBackend.Services; 
namespace InvoicesBackend.API.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly PlanGuardService _planGuard;
    public ProjectsController(ApplicationDbContext context, PlanGuardService planGuard)
    {
        _context = context;
        _planGuard = planGuard;
    }

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

        // Build name→id lookup so we can match by name when ProjectId FK is null
        var projectNameToId = projects.ToDictionary(
            p => p.Name.Trim().ToLower(),
            p => p.Id);

        // Invoices: match by ProjectId OR by project name items
        var invoicesByProjectId = await _context.Invoices
            .Where(i => i.ProjectId != null && projectIds.Contains(i.ProjectId!.Value)
                && i.BusinessId == business.Id)
            .GroupBy(i => i.ProjectId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count(), Total = g.Sum(i => i.TotalAmount) })
            .ToListAsync();

        // Also count invoices linked by name via InvoiceItems
        var invoicesByName = await (
            from item in _context.InvoiceItems
            join inv in _context.Invoices on item.InvoiceId equals inv.Id
            where inv.BusinessId == business.Id
                && inv.ProjectId == null
                && item.ProjectName != null
            select new { item.ProjectName, inv.Id, inv.TotalAmount }
        ).ToListAsync();

        var invoiceCounts = invoicesByProjectId.ToDictionary(x => x.Id, x => new { x.Count, x.Total });
        foreach (var grp in invoicesByName.GroupBy(x => x.ProjectName!.Trim().ToLower()))
        {
            if (projectNameToId.TryGetValue(grp.Key, out var pid) && !invoiceCounts.ContainsKey(pid))
                invoiceCounts[pid] = new { Count = grp.Select(x => x.Id).Distinct().Count(), Total = grp.Sum(x => x.TotalAmount) };
        }

        // Assistants: match by ProjectId OR by project name
        var assignmentsByProjectId = await _context.AssistantAssignments
            .Where(a => a.ProjectId != null && projectIds.Contains(a.ProjectId!.Value)
                && a.BusinessId == business.Id)
            .GroupBy(a => a.ProjectId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync();

        var assignmentsByName = await _context.AssistantAssignments
            .Where(a => a.ProjectId == null && a.BusinessId == business.Id
                && a.ProjectName != null)
            .Select(a => new { a.ProjectName, a.Id })
            .ToListAsync();

        var assistantCounts = assignmentsByProjectId.ToDictionary(x => x.Id, x => new { x.Count });
        foreach (var grp in assignmentsByName.GroupBy(x => x.ProjectName!.Trim().ToLower()))
        {
            if (projectNameToId.TryGetValue(grp.Key, out var pid) && !assistantCounts.ContainsKey(pid))
                assistantCounts[pid] = new { Count = grp.Count() };
        }

        // Bills: match by ProjectId OR by project name
        var billsByProjectId = await _context.Bills
            .Where(b => b.ProjectId != null && projectIds.Contains(b.ProjectId!.Value)
                && b.BusinessId == business.Id)
            .GroupBy(b => b.ProjectId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync();

        var billsByName = await _context.Bills
            .Where(b => b.ProjectId == null && b.BusinessId == business.Id
                && b.ProjectName != null)
            .Select(b => new { b.ProjectName, b.Id })
            .ToListAsync();

        var billCounts = billsByProjectId.ToDictionary(x => x.Id, x => new { x.Count });
        foreach (var grp in billsByName.GroupBy(x => x.ProjectName!.Trim().ToLower()))
        {
            if (projectNameToId.TryGetValue(grp.Key, out var pid) && !billCounts.ContainsKey(pid))
                billCounts[pid] = new { Count = grp.Count() };
        }

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
            TotalInvoiced = invoiceCounts.TryGetValue(p.Id, out var it2) ? it2.Total : 0,
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

    /// <summary>
    /// Returns a full profit/loss analysis for a project:
    /// Revenue (invoiced + collected), Costs (bills + assistant fees + personal expenses),
    /// Profit, Margin, and actionable insights.
    /// </summary>
    [HttpGet("{id}/profit")]
    public async Task<IActionResult> GetProjectProfit(Guid id)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var project = await _context.Projects
            .Include(p => p.Client)
            .FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == business.Id);
        if (project == null) return NotFound();

        // ── Revenue ──────────────────────────────────────────
        var invoices = await _context.Invoices
            .Where(i => i.BusinessId == business.Id
                && (i.ProjectId == id || _context.InvoiceItems
                    .Any(ii => ii.InvoiceId == i.Id && ii.ProjectName == project.Name))
                && i.PaymentStatus != Domain.Enums.PaymentStatus.Draft)
            .ToListAsync();

        var totalInvoiced = invoices.Sum(i => i.TotalAmount);
        var totalCollected = invoices.Sum(i => i.AmountPaid);
        var totalOutstanding = invoices.Sum(i => i.RemainingAmount);

        // ── Bill costs ────────────────────────────────────────
        var bills = await _context.Bills
            .Include(b => b.Items)
            .Where(b => b.BusinessId == business.Id
                && (b.ProjectId == id || b.ProjectName == project.Name))
            .ToListAsync();

        var totalBillCost = bills.SelectMany(b => b.Items).Sum(i => i.TotalCost);
        var totalRefunded  = bills.SelectMany(b => b.Items).Sum(i => i.AmountRefunded);
        var netBillCost    = totalBillCost - totalRefunded;

        var billByBrand = bills
            .GroupBy(b => b.BrandName)
            .Select(g => new { Brand = g.Key, Total = g.SelectMany(b => b.Items).Sum(i => i.TotalCost) })
            .OrderByDescending(x => x.Total)
            .ToList();

        // ── Assistant fees ────────────────────────────────────
        var assignments = await _context.AssistantAssignments
            .Where(a => a.BusinessId == business.Id
                && (a.ProjectId == id || a.ProjectName == project.Name))
            .ToListAsync();

        var totalAssistantFees = assignments.Sum(a => a.Fee);
        var paidAssistantFees  = assignments.Where(a => a.IsPaid).Sum(a => a.Fee);
        var unpaidAssistantFees = totalAssistantFees - paidAssistantFees;

        // ── Personal expenses tagged to this project ──────────
        var personalExpenses = await _context.PersonalExpenses
            .Where(e => e.BusinessId == business.Id && e.ProjectId == id)
            .ToListAsync();

        var totalPersonalExp = personalExpenses.Sum(e => e.Amount);

        var personalByCategory = personalExpenses
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(e => e.Amount) })
            .OrderByDescending(x => x.Total)
            .ToList();

        // ── P&L ───────────────────────────────────────────────
        var totalCosts  = netBillCost + totalAssistantFees + totalPersonalExp;
        var grossProfit = totalInvoiced - totalCosts;
        var netProfit   = totalCollected - totalCosts;
        var margin      = totalInvoiced > 0
            ? Math.Round((grossProfit / totalInvoiced) * 100, 1)
            : 0;

        // ── Insights ──────────────────────────────────────────
        var insights = new List<object>();

        if (totalInvoiced == 0)
            insights.Add(new { Type = "warning", Icon = "⚠️", Message = "No invoices raised for this project yet. Add an invoice to start tracking revenue." });

        if (totalOutstanding > 0)
            insights.Add(new { Type = "action", Icon = "💰", Message = $"₹{totalOutstanding:N0} is still outstanding from the client. Follow up to improve your cash flow." });

        if (unpaidAssistantFees > 0)
            insights.Add(new { Type = "action", Icon = "🧑‍🤝‍🧑", Message = $"₹{unpaidAssistantFees:N0} in assistant fees are unpaid. Clear these to get an accurate net profit." });

        if (margin < 0)
            insights.Add(new { Type = "danger", Icon = "🔴", Message = $"This project is running at a loss (margin: {margin}%). Your costs (₹{totalCosts:N0}) exceed revenue (₹{totalInvoiced:N0})." });
        else if (margin < 20)
            insights.Add(new { Type = "warning", Icon = "🟡", Message = $"Low margin ({margin}%). Consider increasing your invoice amount or reducing bill costs to improve profitability." });
        else if (margin >= 50)
            insights.Add(new { Type = "success", Icon = "🟢", Message = $"Excellent margin ({margin}%)! This is one of your most profitable project types." });

        if (totalRefunded > 0)
            insights.Add(new { Type = "info", Icon = "🔄", Message = $"₹{totalRefunded:N0} was refunded from bill items — this has been deducted from your costs." });

        if (billByBrand.Any())
        {
            var topBrand = billByBrand.First();
            if (topBrand.Total > totalInvoiced * 0.4m)
                insights.Add(new { Type = "warning", Icon = "🏷️", Message = $"{topBrand.Brand} accounts for ₹{topBrand.Total:N0} ({Math.Round((topBrand.Total / (totalInvoiced == 0 ? 1 : totalInvoiced)) * 100, 0)}% of revenue). Consider negotiating better rates." });
        }

        if (project.Budget.HasValue && project.Budget > 0)
        {
            var budgetUsed = Math.Round((totalCosts / project.Budget.Value) * 100, 1);
            if (budgetUsed > 100)
                insights.Add(new { Type = "danger", Icon = "📊", Message = $"Over budget! Spent ₹{totalCosts:N0} against a budget of ₹{project.Budget.Value:N0} ({budgetUsed}% used)." });
            else if (budgetUsed > 80)
                insights.Add(new { Type = "warning", Icon = "📊", Message = $"Budget at {budgetUsed}% used (₹{totalCosts:N0} of ₹{project.Budget.Value:N0}). Watch your remaining spend." });
            else
                insights.Add(new { Type = "success", Icon = "📊", Message = $"On track with budget — {budgetUsed}% used (₹{totalCosts:N0} of ₹{project.Budget.Value:N0})." });
        }

        if (!insights.Any())
            insights.Add(new { Type = "success", Icon = "✅", Message = "Project finances look healthy. Keep it up!" });

        return Ok(new
        {
            ProjectId   = project.Id,
            ProjectName = project.Name,
            ClientName  = project.Client?.ClientName,
            Budget      = project.Budget,

            Revenue = new
            {
                TotalInvoiced = totalInvoiced,
                TotalCollected = totalCollected,
                TotalOutstanding = totalOutstanding,
                InvoiceCount = invoices.Count
            },
            Costs = new
            {
                BillCost       = netBillCost,
                BillCostGross  = totalBillCost,
                TotalRefunded  = totalRefunded,
                AssistantFees  = totalAssistantFees,
                PersonalExpenses = totalPersonalExp,
                Total          = totalCosts,
                ByBrand        = billByBrand,
                ByExpenseCategory = personalByCategory
            },
            Profit = new
            {
                GrossProfit = grossProfit,
                NetProfit   = netProfit,
                Margin      = margin
            },
            Insights = insights
        });
    }


    [HttpPost]
    public async Task<IActionResult> Create(CreateProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Project name is required");

        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var uidC = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(uidC, out var ownerUidP))
        {
            var projectLimitErr = await _planGuard.CheckProjectLimitAsync(business.Id, ownerUidP);
            if (projectLimitErr != null) return BadRequest(projectLimitErr);
        }

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
        if (!Guid.TryParse(userIdClaim, out var userId))
            return null;
        return await _context.Businesses.FirstOrDefaultAsync(x => x.UserId == userId);
    }
}
