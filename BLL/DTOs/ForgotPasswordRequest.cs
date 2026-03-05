using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
