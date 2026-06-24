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

    public AuthController(
        ApplicationDbContext context,
        JwtService jwtService,
        ExternalAuthService externalAuthService)
    {
        _context = context;
        _jwtService = jwtService;
        _externalAuthService = externalAuthService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (await _context.Users.AnyAsync(x => x.Email == request.Email))
            return BadRequest("Email already exists");

        if (await _context.Users.AnyAsync(x => x.Username == request.Username))
            return BadRequest("Username already exists");

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Username = request.Username,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.BusinessOwner
        };

        var business = new Business
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BusinessName = request.BusinessName,
            BusinessEmail = request.BusinessEmail
        };

        _context.Users.Add(user);
        _context.Businesses.Add(business);

        await _context.SaveChangesAsync();

        return Ok("Registered successfully");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Email == request.Email);

        if (user == null)
            return Unauthorized("Invalid credentials");

        var isValid = BCrypt.Net.BCrypt.Verify(
            request.Password,
            user.PasswordHash);

        if (!isValid)
            return Unauthorized("Invalid credentials");

        var token = _jwtService.GenerateToken(user);

        return Ok(new { token });
    }

    /// <summary>
    /// Sign in (or sign up, on first use) with Google, Facebook, or Apple.
    /// The frontend obtains a token from the provider's SDK and sends it
    /// here for verification; on success we issue our own JWT just like
    /// a normal login.
    /// </summary>
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
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Provider not configured on the server side.
            return StatusCode(StatusCodes.Status501NotImplemented, ex.Message);
        }

        // 1) Match an existing account already linked to this provider.
        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.AuthProvider == info.Provider && u.ExternalId == info.ExternalId);

        // 2) Otherwise, match by email — links this provider to an existing
        //    email/password (or other-provider) account.
        if (user == null)
        {
            user = await _context.Users.FirstOrDefaultAsync(u => u.Email == info.Email);
            if (user != null)
            {
                user.AuthProvider = info.Provider;
                user.ExternalId = info.ExternalId;
            }
        }

        // 3) Otherwise, create a brand new account + business.
        if (user == null)
        {
            var fullName = !string.IsNullOrWhiteSpace(request.FullName)
                ? request.FullName!
                : (!string.IsNullOrWhiteSpace(info.FullName) ? info.FullName! : info.Email.Split('@')[0]);

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

            var business = new Business
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                BusinessName = fullName,
                BusinessEmail = info.Email
            };

            _context.Users.Add(user);
            _context.Businesses.Add(business);
        }

        if (!user.IsActive)
            return Unauthorized("This account has been deactivated");

        await _context.SaveChangesAsync();

        var token = _jwtService.GenerateToken(user);
        return Ok(new { token });
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