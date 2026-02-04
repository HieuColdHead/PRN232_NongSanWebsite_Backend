using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public sealed class EmailOtpRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; init; } = string.Empty;
}
