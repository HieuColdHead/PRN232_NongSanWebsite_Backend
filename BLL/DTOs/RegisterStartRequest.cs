using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public sealed class RegisterStartRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; init; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; init; }

    [MaxLength(150)]
    public string? DisplayName { get; init; }

    [Required]
    [MinLength(6)]
    [MaxLength(100)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; init; } = string.Empty;
}
