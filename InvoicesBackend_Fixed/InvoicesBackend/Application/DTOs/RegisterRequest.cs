using System.ComponentModel.DataAnnotations;

namespace InvoicesBackend.Application.DTOs;

public class RegisterRequest
{
    [Required]
    [MaxLength(150)]
    public string? FullName { get; set; }

    [Required]
    [MaxLength(80)]
    public string? Username { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string? Email { get; set; }

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string? Password { get; set; }

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [Required]
    [MaxLength(180)]
    public string? BusinessName { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string? BusinessEmail { get; set; }
    public string? OtpCode { get; set; }
}
