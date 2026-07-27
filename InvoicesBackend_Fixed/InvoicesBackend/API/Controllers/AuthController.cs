using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicesBackend.Persistence;
using InvoicesBackend.Application.DTOs;
using InvoicesBackend.Domain.Entities;
using InvoicesBackend.Domain.Enums;
using InvoicesBackend.Services;

namespace InvoicesBackend.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly JwtService _jwtService;
    private readonly ExternalAuthService _externalAuthService;
    private readonly EmailService _emailService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ApplicationDbContext context,
        JwtService jwtService,
        ExternalAuthService externalAuthService,
        EmailService emailService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _externalAuthService = externalAuthService;
        _emailService = emailService;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════
    // OTP — send and verify (used by both register and reset)
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Generates a 6-digit OTP and emails it.
    /// Rate-limited to once per 90 seconds per email+type combination.
    /// </summary>
    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp(SendOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return BadRequest("A valid email address is required");

        var email = request.Email.Trim().ToLowerInvariant();
        var type = request.Type == "password_reset" ? "password_reset" : "registration";

        // For registration: email must not already have an active account
        if (type == "registration" && await _context.Users.AnyAsync(u => u.Email == email))
            return BadRequest("An account with this email already exists");

        // For password reset: email must exist
        if (type == "password_reset" && !await _context.Users.AnyAsync(u => u.Email == email))
        {
            // Don't reveal whether the email exists — return success silently
            return Ok(new { message = "If that email exists, you'll receive an OTP shortly." });
        }

        // Rate limit: only allow resend after 90 seconds
        var recent = await _context.OtpVerifications
            .Where(o => o.Email == email && o.Type == type && !o.IsUsed)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (recent != null && recent.CreatedAt > DateTime.UtcNow.AddSeconds(-90))
        {
            var waitSeconds = (int)(recent.CreatedAt.AddSeconds(90) - DateTime.UtcNow).TotalSeconds;
            return BadRequest($"Please wait {waitSeconds} seconds before requesting another OTP");
        }

        // Invalidate all previous OTPs for this email+type
        var oldOtps = await _context.OtpVerifications
            .Where(o => o.Email == email && o.Type == type && !o.IsUsed)
            .ToListAsync();
        foreach (var old in oldOtps) old.IsUsed = true;

        // Generate 6-digit OTP
        var otp = Random.Shared.Next(100000, 999999).ToString();
        var otpHash = BCrypt.Net.BCrypt.HashPassword(otp, workFactor: 10);

        // DEV: log OTP to console so it's visible without real email
        _logger.LogWarning("🔑 OTP for {Email} [{Type}]: {OTP} (expires in 10 mins)", email, type, otp);

        _context.OtpVerifications.Add(new OtpVerification
        {
            Id = Guid.NewGuid(),
            Email = email,
            Type = type,
            CodeHash = otpHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        try
        {
            await _emailService.SendOtpAsync(email, otp, type);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to send email: {ex.Message}. " +
                "In Development mode, check the console for the OTP.");
        }

        return Ok(new { message = "OTP sent successfully" });
    }

    /// <summary>Verifies an OTP without completing any action (used by registration step).</summary>
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpRequest request)
    {
        var (valid, error) = await ValidateOtpAsync(
            request.Email?.Trim().ToLowerInvariant() ?? "",
            request.Code?.Trim() ?? "",
            request.Type == "password_reset" ? "password_reset" : "registration",
            consume: false);

        if (!valid) return BadRequest(error);
        return Ok(new { valid = true });
    }

    // ══════════════════════════════════════════════════════
    // REGISTER (OTP-verified)
    // ══════════════════════════════════════════════════════

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))   return BadRequest("Full name is required");
        if (string.IsNullOrWhiteSpace(request.Username))   return BadRequest("Username is required");
        if (string.IsNullOrWhiteSpace(request.Email))      return BadRequest("Email is required");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest("Password must be at least 8 characters");
        if (string.IsNullOrWhiteSpace(request.BusinessName)) return BadRequest("Business name is required");
        if (string.IsNullOrWhiteSpace(request.OtpCode))    return BadRequest("OTP is required");

        var email = request.Email.Trim().ToLowerInvariant();
        var username = request.Username.Trim();

        if (await _context.Users.AnyAsync(x => x.Email == email))
            return BadRequest("Email already exists");

        if (await _context.Users.AnyAsync(x => x.Username == username))
            return BadRequest("Username already exists");

        // Verify OTP
        var (valid, error) = await ValidateOtpAsync(email, request.OtpCode.Trim(), "registration", consume: true);
        if (!valid) return BadRequest(error ?? "Invalid or expired OTP");

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Username = username,
            Email = email,
            PhoneNumber = request.PhoneNumber ?? string.Empty,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.BusinessOwner
        };

        var business = new Business
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BusinessName = request.BusinessName.Trim(),
            BusinessEmail = (request.BusinessEmail ?? email).Trim().ToLowerInvariant()
        };

        _context.Users.Add(user);
        _context.Businesses.Add(business);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Account created successfully" });
    }

    // ══════════════════════════════════════════════════════
    // LOGIN
    // ══════════════════════════════════════════════════════

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Email and password are required");

        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Email == request.Email.Trim().ToLowerInvariant());

        if (user == null) return Unauthorized("Invalid credentials");
        if (!user.IsActive) return Unauthorized("This account has been deactivated");

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
            return Unauthorized("This account uses social sign-in. Please log in with Google, Facebook, or Apple.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials");

        // Include assistantId in token for dual-mode support
        var assistantForLogin = await _context.Assistants
            .FirstOrDefaultAsync(a => a.UserId == user.Id);

        return Ok(new { token = _jwtService.GenerateToken(user, assistantForLogin?.Id) });
    }

    // ══════════════════════════════════════════════════════
    // FORGOT PASSWORD — sends OTP (same as send-otp but sugar endpoint)
    // ══════════════════════════════════════════════════════

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Email is required");

        return await SendOtp(new SendOtpRequest
        {
            Email = request.Email,
            Type = "password_reset"
        });
    }

    // ══════════════════════════════════════════════════════
    // RESET PASSWORD (via OTP — public, no auth)
    // ══════════════════════════════════════════════════════

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))      return BadRequest("Email is required");
        if (string.IsNullOrWhiteSpace(request.OtpCode))    return BadRequest("OTP is required");
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest("New password must be at least 8 characters");

        var email = request.Email.Trim().ToLowerInvariant();

        var (valid, error) = await ValidateOtpAsync(email, request.OtpCode.Trim(), "password_reset", consume: true);
        if (!valid) return BadRequest(error ?? "Invalid or expired OTP");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound("Account not found");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Password updated successfully" });
    }

    // ══════════════════════════════════════════════════════
    // CHANGE PASSWORD (authenticated — from profile)
    // ══════════════════════════════════════════════════════

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            return BadRequest("Current password is required");
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest("New password must be at least 8 characters");

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
            return BadRequest("This account uses social sign-in and has no password to change.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest("Current password is incorrect");

        if (request.CurrentPassword == request.NewPassword)
            return BadRequest("New password must be different from current password");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Password changed successfully" });
    }

    // ══════════════════════════════════════════════════════
    // EXTERNAL LOGIN (Google / Facebook / Apple)
    // ══════════════════════════════════════════════════════

    [HttpPost("external-login")]
    public async Task<IActionResult> ExternalLogin(ExternalLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.Token))
            return BadRequest("Provider and token are required");

        ExternalUserInfo info;
        try
        {
            info = await _externalAuthService.VerifyAsync(request.Provider, request.Token);
        }
        catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        catch (InvalidOperationException ex)   { return StatusCode(501, ex.Message); }

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.AuthProvider == info.Provider && u.ExternalId == info.ExternalId);

        if (user == null)
        {
            user = await _context.Users.FirstOrDefaultAsync(u => u.Email == info.Email);
            if (user != null) { user.AuthProvider = info.Provider; user.ExternalId = info.ExternalId; }
        }

        if (user == null)
        {
            var fullName = !string.IsNullOrWhiteSpace(request.FullName) ? request.FullName!
                : !string.IsNullOrWhiteSpace(info.FullName) ? info.FullName!
                : info.Email.Split('@')[0];

            user = new User
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Username = await GenerateUniqueUsernameAsync(info.Email),
                Email = info.Email,
                PhoneNumber = string.Empty,
                PasswordHash = null,
                AuthProvider = info.Provider,
                ExternalId = info.ExternalId,
                Role = UserRole.BusinessOwner
            };
            _context.Users.Add(user);
            _context.Businesses.Add(new Business { Id = Guid.NewGuid(), UserId = user.Id, BusinessName = fullName, BusinessEmail = info.Email });
        }

        if (!user.IsActive) return Unauthorized("This account has been deactivated");

        await _context.SaveChangesAsync();

        var assistantForExt = await _context.Assistants
            .FirstOrDefaultAsync(a => a.UserId == user.Id);

        return Ok(new { token = _jwtService.GenerateToken(user, assistantForExt?.Id) });
    }

    // ══════════════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Validates an OTP. Returns (true, null) on success.
    /// If consume=true, marks the OTP as used on success.
    /// Increments AttemptCount and blocks at 5 failed attempts.
    /// </summary>
    private async Task<(bool valid, string? error)> ValidateOtpAsync(
        string email, string code, string type, bool consume)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            return (false, "Email and OTP code are required");

        var otp = await _context.OtpVerifications
            .Where(o => o.Email == email && o.Type == type && !o.IsUsed)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null)
            return (false, "No active OTP found. Please request a new one.");

        if (otp.ExpiresAt < DateTime.UtcNow)
        {
            otp.IsUsed = true;
            await _context.SaveChangesAsync();
            return (false, "OTP has expired. Please request a new one.");
        }

        if (otp.AttemptCount >= 5)
            return (false, "Too many failed attempts. Please request a new OTP.");

        if (!BCrypt.Net.BCrypt.Verify(code, otp.CodeHash))
        {
            otp.AttemptCount++;
            await _context.SaveChangesAsync();
            var remaining = 5 - otp.AttemptCount;
            return (false, $"Incorrect OTP. {remaining} attempt{(remaining == 1 ? "" : "s")} remaining.");
        }

        if (consume)
        {
            otp.IsUsed = true;
            await _context.SaveChangesAsync();
        }

        return (true, null);
    }

    private async Task<string> GenerateUniqueUsernameAsync(string email)
    {
        var baseName = email.Split('@')[0];
        var candidate = baseName;
        var suffix = 0;
        while (await _context.Users.AnyAsync(u => u.Username == candidate))
        {
            suffix++;
            candidate = $"{baseName}{suffix}";
        }
        return candidate;
    }
}
