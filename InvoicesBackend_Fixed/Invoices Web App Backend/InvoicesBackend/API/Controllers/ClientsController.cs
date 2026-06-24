using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicesBackend.Application.DTOs;
using InvoicesBackend.Domain.Entities;
using InvoicesBackend.Persistence;

namespace InvoicesBackend.API.Controllers;

/// <summary>
/// Lets the frontend show a list of clients the business has previously
/// invoiced (for quick "select a frequent client" autofill when creating
/// a new invoice), and the line items from a client's most recent invoice.
/// </summary>
[ApiController]
[Route("api/clients")]
[Authorize]
public class ClientsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ClientsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateClient(UpdateClientRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientName))
            return BadRequest("Client name is required");

        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        // Prevent duplicate by name+email for this business
        var existing = await _context.Clients
            .FirstOrDefaultAsync(c => c.BusinessId == business.Id
                && c.ClientName == request.ClientName.Trim());
        if (existing != null)
            return BadRequest("A client with this name already exists");

        var client = new Client
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            ClientName = request.ClientName.Trim(),
            ClientEmail = request.ClientEmail?.Trim(),
            ClientPhone = request.ClientPhone?.Trim(),
            ClientAddress = request.ClientAddress?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        var invoices = new List<Invoice>();
        return Ok(MapToSummary(client, invoices));
    }

    [HttpGet]
    public async Task<IActionResult> GetClients([FromQuery] string? search)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var clients = await _context.Clients
            .Where(c => c.BusinessId == business.Id)
            .ToListAsync();

        var invoices = await _context.Invoices
            .Where(i => i.BusinessId == business.Id)
            .ToListAsync();

        var result = clients.Select(c => MapToSummary(c, invoices))
            .OrderByDescending(c => c.InvoiceCount)
            .ThenByDescending(c => c.LastInvoiceDate)
            .ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            result = result.Where(c =>
                c.ClientName.ToLowerInvariant().Contains(term) ||
                (c.ClientEmail ?? "").ToLowerInvariant().Contains(term) ||
                (c.ClientPhone ?? "").ToLowerInvariant().Contains(term)
            ).ToList();
        }

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetClient(Guid id)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.Id == id && c.BusinessId == business.Id);

        if (client == null) return NotFound("Client not found");

        var invoices = await _context.Invoices
            .Where(i => i.BusinessId == business.Id && i.ClientId == id)
            .ToListAsync();

        return Ok(MapToSummary(client, invoices));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateClient(Guid id, UpdateClientRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientName))
            return BadRequest("Client name is required");

        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.Id == id && c.BusinessId == business.Id);

        if (client == null) return NotFound("Client not found");

        client.ClientName = request.ClientName.Trim();
        client.ClientEmail = request.ClientEmail?.Trim();
        client.ClientPhone = request.ClientPhone?.Trim();
        client.ClientAddress = request.ClientAddress?.Trim();

        await _context.SaveChangesAsync();

        var invoices = await _context.Invoices
            .Where(i => i.BusinessId == business.Id && i.ClientId == id)
            .ToListAsync();

        return Ok(MapToSummary(client, invoices));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteClient(Guid id)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.Id == id && c.BusinessId == business.Id);

        if (client == null) return NotFound("Client not found");

        var hasInvoices = await _context.Invoices
            .AnyAsync(i => i.BusinessId == business.Id && i.ClientId == id);

        if (hasInvoices)
            return BadRequest("Cannot delete a client that has invoices. Consider keeping them for your records.");

        _context.Clients.Remove(client);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Client removed" });
    }

    private static ClientSummaryResponse MapToSummary(Client c, List<Invoice> invoices)
    {
        var clientInvoices = invoices.Where(i => i.ClientId == c.Id).ToList();

        return new ClientSummaryResponse
        {
            Id = c.Id,
            ClientName = c.ClientName ?? string.Empty,
            ClientEmail = c.ClientEmail,
            ClientPhone = c.ClientPhone,
            ClientAddress = c.ClientAddress,
            InvoiceCount = clientInvoices.Count,
            TotalRevenue = clientInvoices.Sum(i => i.AmountPaid),
            PendingAmount = clientInvoices.Sum(i => i.RemainingAmount),
            LastInvoiceDate = clientInvoices
                .Select(i => (DateTime?)i.InvoiceDate)
                .OrderByDescending(d => d)
                .FirstOrDefault() ?? c.CreatedAt
        };
    }

    [HttpGet("{id}/last-items")]
    public async Task<IActionResult> GetLastInvoiceItems(Guid id)
    {
        var business = await GetBusinessAsync();
        if (business == null) return BadRequest("Business not found");

        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.Id == id && c.BusinessId == business.Id);

        if (client == null) return NotFound("Client not found");

        var lastInvoice = await _context.Invoices
            .Where(i => i.ClientId == id && i.BusinessId == business.Id)
            .OrderByDescending(i => i.InvoiceDate)
            .FirstOrDefaultAsync();

        if (lastInvoice == null)
            return Ok(new List<LastInvoiceItemResponse>());

        var items = await _context.InvoiceItems
            .Where(x => x.InvoiceId == lastInvoice.Id)
            .Select(x => new LastInvoiceItemResponse
            {
                ExpenseName = x.ExpenseName ?? string.Empty,
                ProjectName = x.ProjectName,
                Amount = x.Amount,
                Quantity = x.Quantity
            })
            .ToListAsync();

        return Ok(items);
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
