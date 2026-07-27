using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicesBackend.Domain.Entities;
using InvoicesBackend.Persistence;

namespace InvoicesBackend.API.Controllers;

[ApiController]
[Route("api/personal-expenses")]
[Authorize]
public class PersonalExpensesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PersonalExpensesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public static readonly string[] Categories =
    [
        "Travel", "Food & Dining", "Equipment", "Software & Tools",
        "Rent & Utilities", "Marketing", "Clothing & Styling",
        "Communication", "Health", "Education", "Other"
    ];

    [HttpGet]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] string? category,
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var query = _context.PersonalExpenses
            .Where(e => e.BusinessId == business.Id);

        if (!string.IsNullOrWhiteSpace(category) && category != "All")
            query = query.Where(e => e.Category == category);

        if (DateTime.TryParse(from, out var fromDate))
            query = query.Where(e => e.ExpenseDate >= fromDate.ToUniversalTime());

        if (Guid.TryParse(HttpContext.Request.Query["projectId"].FirstOrDefault(), out var filterProjectId))
            query = query.Where(e => e.ProjectId == filterProjectId);

        if (DateTime.TryParse(to, out var toDate))
            query = query.Where(e => e.ExpenseDate <= toDate.ToUniversalTime().AddDays(1));

        var expenses = await query
            .OrderByDescending(e => e.ExpenseDate)
            .ToListAsync();

        var total = expenses.Sum(e => e.Amount);

        var byCategory = expenses
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Amount), Count = g.Count() })
            .OrderByDescending(x => x.Total)
            .ToList();

        return Ok(new
        {
            Total = total,
            ByCategory = byCategory,
            Items = expenses.Select(e => new
            {
                e.Id,
                e.Description,
                e.Category,
                e.Amount,
                e.ExpenseDate,
                e.Notes,
                e.ProjectId,
                e.ProjectName,
                e.CreatedAt
            })
        });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var thisMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var lastMonth = thisMonth.AddMonths(-1);

        var allExpenses = await _context.PersonalExpenses
            .Where(e => e.BusinessId == business.Id)
            .ToListAsync();

        return Ok(new
        {
            TotalAllTime = allExpenses.Sum(e => e.Amount),
            TotalThisMonth = allExpenses.Where(e => e.ExpenseDate >= thisMonth).Sum(e => e.Amount),
            TotalLastMonth = allExpenses.Where(e => e.ExpenseDate >= lastMonth && e.ExpenseDate < thisMonth).Sum(e => e.Amount),
            Count = allExpenses.Count,
            TopCategory = allExpenses
                .GroupBy(e => e.Category)
                .OrderByDescending(g => g.Sum(x => x.Amount))
                .Select(g => g.Key)
                .FirstOrDefault() ?? "—",
            Categories = Categories
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateExpense(CreatePersonalExpenseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest("Description is required");
        if (request.Amount <= 0)
            return BadRequest("Amount must be greater than 0");

        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var expense = new PersonalExpense
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            Description = request.Description.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Other" : request.Category.Trim(),
            Amount = request.Amount,
            ExpenseDate = DateTime.SpecifyKind(request.ExpenseDate.Date, DateTimeKind.Utc),
            Notes = request.Notes?.Trim(),
            ProjectId = request.ProjectId,
            ProjectName = request.ProjectName?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.PersonalExpenses.Add(expense);
        await _context.SaveChangesAsync();

        return Ok(new { expense.Id, expense.Description, expense.Category, expense.Amount, expense.ExpenseDate });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateExpense(Guid id, CreatePersonalExpenseRequest request)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var expense = await _context.PersonalExpenses
            .FirstOrDefaultAsync(e => e.Id == id && e.BusinessId == business.Id);
        if (expense == null) return NotFound();

        expense.Description = request.Description?.Trim() ?? expense.Description;
        expense.Category = string.IsNullOrWhiteSpace(request.Category) ? expense.Category : request.Category.Trim();
        expense.Amount = request.Amount > 0 ? request.Amount : expense.Amount;
        expense.ExpenseDate = DateTime.SpecifyKind(request.ExpenseDate.Date, DateTimeKind.Utc);
        expense.Notes = request.Notes?.Trim();
        expense.ProjectId = request.ProjectId;
        expense.ProjectName = string.IsNullOrWhiteSpace(request.ProjectName) ? expense.ProjectName : request.ProjectName.Trim();

        await _context.SaveChangesAsync();
        return Ok(new { expense.Id, expense.Description, expense.Category, expense.Amount, expense.ExpenseDate });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteExpense(Guid id)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var expense = await _context.PersonalExpenses
            .FirstOrDefaultAsync(e => e.Id == id && e.BusinessId == business.Id);
        if (expense == null) return NotFound();

        _context.PersonalExpenses.Remove(expense);
        await _context.SaveChangesAsync();
        return Ok();
    }


    [HttpGet("projects-used")]
    public async Task<IActionResult> GetProjectsUsed()
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var projects = await _context.Projects
            .Where(p => p.BusinessId == business.Id)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync();

        return Ok(projects);
    }

    private async Task<Business?> GetBusinessAsync()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var userId)) return null;
        return await _context.Businesses.FirstOrDefaultAsync(b => b.UserId == userId);
    }
}

public class CreatePersonalExpenseRequest
{
    public string Description { get; set; } = string.Empty;
    public string? Category { get; set; }
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? Notes { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
}
