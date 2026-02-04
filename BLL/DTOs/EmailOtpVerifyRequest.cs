using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public sealed class EmailOtpVerifyRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(4)]
    [MaxLength(10)]
    public string Otp { get; init; } = string.Empty;

    [MaxLength(150)]
    public string? DisplayName { get; init; }

    [MaxLength(20)]
    public string? PhoneNumber { get; init; }
}
