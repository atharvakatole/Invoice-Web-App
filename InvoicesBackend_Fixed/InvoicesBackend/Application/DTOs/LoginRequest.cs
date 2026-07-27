using System.ComponentModel.DataAnnotations;

namespace InvoicesBackend.Application.DTOs;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string? Email { get; set; }

    [Required]
    public string? Password { get; set; }
}
