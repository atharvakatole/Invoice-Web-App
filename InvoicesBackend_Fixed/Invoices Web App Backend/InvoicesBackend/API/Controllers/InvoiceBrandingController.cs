using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicesBackend.Application.DTOs;
using InvoicesBackend.Domain.Entities;
using InvoicesBackend.Persistence;

namespace InvoicesBackend.API.Controllers;

/// <summary>
/// "Design it yourself" invoice branding: logo, accent color, layout style,
/// and the footer signature/payment-details text shown on every generated
/// invoice (when no fully custom PDF template has been uploaded).
/// </summary>
[ApiController]
[Route("api/invoice-branding")]
[Authorize]
public class InvoiceBrandingController : ControllerBase
{
    private const long MaxLogoSizeBytes = 2 * 1024 * 1024; // 2 MB

    private static readonly List<TemplateStyleOption> Styles = new()
    {
        new TemplateStyleOption
        {
            Key = "modern",
            Name = "Modern",
            Description = "Bold accent-colored header band with your logo and invoice details in white."
        },
        new TemplateStyleOption
        {
            Key = "classic",
            Name = "Classic",
            Description = "Elegant serif-style layout with a large 'INVOICE' title and clean dividers — similar to a boutique studio invoice."
        },
        new TemplateStyleOption
        {
            Key = "minimal",
            Name = "Minimal",
            Description = "Clean and understated, with a thin accent-colored rule and minimal color use throughout."
        },
    };

    private readonly ApplicationDbContext _context;

    public InvoiceBrandingController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("styles")]
    [AllowAnonymous]
    public IActionResult GetStyles() => Ok(Styles);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var business = await GetBusinessAsync();
        if (business == null)
            return BadRequest("Business not found");

        var branding = await _context.InvoiceBrandings
            .FirstOrDefaultAsync(x => x.BusinessId == business.Id);

        if (branding == null)
            return Ok(new InvoiceBrandingResponse { HasBranding = false });

        return Ok(ToResponse(branding));
    }

    [HttpGet("my-logo")]
    public async Task<IActionResult> GetMyLogo()
    {
        var business = await GetBusinessAsync();
        if (business == null)
            return BadRequest("Business not found");

        var branding = await _context.InvoiceBrandings
            .FirstOrDefaultAsync(x => x.BusinessId == business.Id);

        if (branding?.LogoData == null || branding.LogoData.Length == 0)
            return NotFound();

        return File(branding.LogoData, branding.LogoContentType ?? "image/png");
    }

    [HttpPost]
    [RequestSizeLimit(MaxLogoSizeBytes + 1024 * 1024)]
    public async Task<IActionResult> Save(
        [FromForm] string templateStyle,
        [FromForm] string accentColor,
        [FromForm] string footerName,
        [FromForm] string footerTitle,
        [FromForm] string footerSubtitle,
        [FromForm] string paymentDetails,
        IFormFile? logo)
    {
        if (!Styles.Any(s => s.Key == templateStyle))
            return BadRequest("Invalid template style");

        if (string.IsNullOrWhiteSpace(accentColor) || !accentColor.StartsWith("#"))
            return BadRequest("Accent color must be a hex value, e.g. #4F7CFF");

        var business = await GetBusinessAsync();
        if (business == null)
            return BadRequest("Business not found");

        var branding = await _context.InvoiceBrandings
            .FirstOrDefaultAsync(x => x.BusinessId == business.Id);

        if (branding == null)
        {
            branding = new InvoiceBranding
            {
                Id = Guid.NewGuid(),
                BusinessId = business.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.InvoiceBrandings.Add(branding);
        }

        branding.TemplateStyle = templateStyle;
        branding.AccentColor = accentColor;
        branding.FooterName = footerName ?? string.Empty;
        branding.FooterTitle = footerTitle ?? string.Empty;
        branding.FooterSubtitle = footerSubtitle ?? string.Empty;
        branding.PaymentDetails = paymentDetails ?? string.Empty;
        branding.UpdatedAt = DateTime.UtcNow;

        if (logo != null && logo.Length > 0)
        {
            if (logo.Length > MaxLogoSizeBytes)
                return BadRequest("Logo image must be under 2 MB");

            if (logo.ContentType != "image/png" && logo.ContentType != "image/jpeg")
                return BadRequest("Logo must be a PNG or JPEG image");

            using var ms = new MemoryStream();
            await logo.CopyToAsync(ms);
            branding.LogoData = ms.ToArray();
            branding.LogoContentType = logo.ContentType;
        }

        await _context.SaveChangesAsync();

        return Ok(ToResponse(branding));
    }

    [HttpDelete("logo")]
    public async Task<IActionResult> DeleteLogo()
    {
        var business = await GetBusinessAsync();
        if (business == null)
            return BadRequest("Business not found");

        var branding = await _context.InvoiceBrandings
            .FirstOrDefaultAsync(x => x.BusinessId == business.Id);

        if (branding == null)
            return NotFound();

        branding.LogoData = null;
        branding.LogoContentType = null;
        branding.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(ToResponse(branding));
    }

    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        var business = await GetBusinessAsync();
        if (business == null)
            return BadRequest("Business not found");

        var branding = await _context.InvoiceBrandings
            .FirstOrDefaultAsync(x => x.BusinessId == business.Id);

        if (branding == null)
            return NotFound();

        _context.InvoiceBrandings.Remove(branding);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Branding removed. Invoices will use the default layout." });
    }

    private static InvoiceBrandingResponse ToResponse(InvoiceBranding b) => new()
    {
        HasBranding = true,
        TemplateStyle = b.TemplateStyle,
        AccentColor = b.AccentColor,
        HasLogo = b.LogoData is { Length: > 0 },
        FooterName = b.FooterName,
        FooterTitle = b.FooterTitle,
        FooterSubtitle = b.FooterSubtitle,
        PaymentDetails = b.PaymentDetails,
        UpdatedAt = b.UpdatedAt
    };

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
