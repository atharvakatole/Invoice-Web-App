namespace InvoicesBackend.Application.DTOs;

public class RegisterRequest
{
    public string? FullName { get; set; }

    public string? Username { get; set; }

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? PhoneNumber { get; set; }

    public string? BusinessName { get; set; }

    public string? BusinessEmail { get; set; }
}