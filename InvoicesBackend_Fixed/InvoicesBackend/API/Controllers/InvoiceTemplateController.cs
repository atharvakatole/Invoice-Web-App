using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoiceTemplateEngine;
using InvoiceTemplateEngine.Models;
using InvoicesBackend.Application.DTOs;
using InvoicesBackend.Domain.Entities;
using InvoicesBackend.Persistence;

namespace InvoicesBackend.API.Controllers;

/// <summary>
/// Lets a business owner upload a PDF in their preferred invoice layout.
/// Generated invoices (preview/download) will then be rendered on top of
/// that PDF, preserving its branding, colors and layout.
/// </summary>
[ApiController]
[Route("api/invoice-template")]
[Authorize]
public class InvoiceTemplateController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private readonly ApplicationDbContext _context;

    public InvoiceTemplateController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        if (file.Length > MaxFileSizeBytes)
            return BadRequest("File is too large. Maximum size is 10 MB");

        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)
            && file.ContentType != "application/pdf")
            return BadRequest("Only PDF files are supported");

        var business = await GetBusinessAsync();
        if (business == null)
            return BadRequest("Business not found");

        byte[] pdfBytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            pdfBytes = ms.ToArray();
        }

        InvoiceTemplateDefinition definition;
        try
        {
            definition = PdfTemplateAnalyzer.Analyze(pdfBytes);
        }
        catch (Exception ex)
        {
            return BadRequest($"Could not read this PDF: {ex.Message}");
        }

        var templateJson = JsonSerializer.Serialize(definition);

        var existing = await _context.InvoiceTemplates
            .FirstOrDefaultAsync(x => x.BusinessId == business.Id);

        if (existing == null)
        {
            existing = new InvoiceTemplate
            {
                Id = Guid.NewGuid(),
                BusinessId = business.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.InvoiceTemplates.Add(existing);
        }

        existing.FileName = file.FileName;
        existing.PdfData = pdfBytes;
        existing.TemplateJson = templateJson;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new InvoiceTemplateStatusResponse
        {
            HasTemplate = true,
            FileName = existing.FileName,
            UpdatedAt = existing.UpdatedAt,
            DetectedFields = definition.DetectedAnchors,
            MissingFields = definition.MissingAnchors
        });
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var business = await GetBusinessAsync();
        if (business == null)
            return BadRequest("Business not found");

        var template = await _context.InvoiceTemplates
            .FirstOrDefaultAsync(x => x.BusinessId == business.Id);

        if (template == null)
            return Ok(new InvoiceTemplateStatusResponse { HasTemplate = false });

        var definition = JsonSerializer.Deserialize<InvoiceTemplateDefinition>(template.TemplateJson)
            ?? new InvoiceTemplateDefinition();

        return Ok(new InvoiceTemplateStatusResponse
        {
            HasTemplate = true,
            FileName = template.FileName,
            UpdatedAt = template.UpdatedAt,
            DetectedFields = definition.DetectedAnchors,
            MissingFields = definition.MissingAnchors
        });
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview()
    {
        var business = await GetBusinessAsync();
        if (business == null)
            return BadRequest("Business not found");

        var template = await _context.InvoiceTemplates
            .FirstOrDefaultAsync(x => x.BusinessId == business.Id);

        if (template == null)
            return NotFound("No template uploaded");

        return File(template.PdfData, "application/pdf", template.FileName);
    }

    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        var business = await GetBusinessAsync();
        if (business == null)
            return BadRequest("Business not found");

        var template = await _context.InvoiceTemplates
            .FirstOrDefaultAsync(x => x.BusinessId == business.Id);

        if (template == null)
            return NotFound("No template uploaded");

        _context.InvoiceTemplates.Remove(template);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Custom invoice template removed. Invoices will use the default layout." });
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
