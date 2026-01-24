using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public class UpdateUserRequest
{
    [MaxLength(255)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(150)]
    public string? DisplayName { get; set; }

    public bool? IsActive { get; set; }
}
