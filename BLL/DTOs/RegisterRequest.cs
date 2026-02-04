using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public sealed class RegisterRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(6)]
    [MaxLength(100)]
    public string Password { get; init; } = string.Empty;

    [MaxLength(150)]
    public string? DisplayName { get; init; }
}
