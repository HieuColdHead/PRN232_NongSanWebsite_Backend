using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public class CreateUserRequest
{
    [Required]
    [MaxLength(128)]
    public string FirebaseUid { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(150)]
    public string? DisplayName { get; set; }

    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
