using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using InvoicesBackend.Domain.Enums;
using InvoicesBackend.Persistence;

namespace InvoicesBackend.Services;

/// <summary>
/// Centralised subscription plan enforcement.
/// All limit checks go through here so the rules are easy to find and change.
///
/// Plan matrix:
///   Free  — 5 invoices, 3 clients, 1 project; NO bills, NO assistants,
///            NO branding, NO reports, NO calendar
///   Trial — same limits as Free but expires after 14 days
///   Premium — everything unlimited
/// </summary>
public class PlanGuardService
{
    private readonly ApplicationDbContext _context;

    // ── Limits ────────────────────────────────────────────
    public const int FreeInvoiceLimit  = 5;
    public const int FreeClientLimit   = 3;
    public const int FreeProjectLimit  = 1;

    public PlanGuardService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Helper ────────────────────────────────────────────

    public static Guid? GetUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    public async Task<(bool isPremium, bool isTrial, bool isExpired)> GetPlanAsync(Guid userId)
    {
        var u = await _context.Users.FindAsync(userId);
        if (u == null) return (false, false, false);

        var isPremium = u.SubscriptionPlan == SubscriptionPlan.Premium
                        && u.SubscriptionExpiryDate > DateTime.UtcNow;
        var isTrial   = u.SubscriptionPlan == SubscriptionPlan.Trial
                        && u.SubscriptionExpiryDate > DateTime.UtcNow;
        var isExpired = !isPremium && u.SubscriptionExpiryDate < DateTime.UtcNow;

        return (isPremium, isTrial, isExpired);
    }

    /// <summary>Returns null if allowed, or an error message if blocked.</summary>
    public async Task<string?> CheckInvoiceLimitAsync(Guid businessId, Guid userId)
    {
        var (isPremium, isTrial, _) = await GetPlanAsync(userId);
        if (isPremium || isTrial) return null;

        var count = await _context.Invoices
            .CountAsync(i => i.BusinessId == businessId
                && i.PaymentStatus != Domain.Enums.PaymentStatus.Draft);

        return count >= FreeInvoiceLimit
            ? $"Free plan limit reached ({FreeInvoiceLimit} invoices). Upgrade to Premium for unlimited invoices."
            : null;
    }

    public async Task<string?> CheckClientLimitAsync(Guid businessId, Guid userId)
    {
        var (isPremium, isTrial, _) = await GetPlanAsync(userId);
        if (isPremium || isTrial) return null;

        var count = await _context.Clients
            .CountAsync(c => c.BusinessId == businessId);

        return count >= FreeClientLimit
            ? $"Free plan limit reached ({FreeClientLimit} clients). Upgrade to Premium for unlimited clients."
            : null;
    }

    public async Task<string?> CheckProjectLimitAsync(Guid businessId, Guid userId)
    {
        var (isPremium, isTrial, _) = await GetPlanAsync(userId);
        if (isPremium || isTrial) return null;

        var count = await _context.Projects
            .CountAsync(p => p.BusinessId == businessId);

        return count >= FreeProjectLimit
            ? $"Free plan limit reached ({FreeProjectLimit} project). Upgrade to Premium for unlimited projects."
            : null;
    }

    public async Task<string?> RequirePremiumAsync(Guid userId, string featureName)
    {
        var (isPremium, isTrial, _) = await GetPlanAsync(userId);
        if (isPremium || isTrial) return null;

        return $"{featureName} is a Premium feature. Upgrade your plan to use it.";
    }
}
