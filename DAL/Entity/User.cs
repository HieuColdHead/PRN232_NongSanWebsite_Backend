using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("Users")]
public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [EmailAddress]
    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(150)]
    public string? DisplayName { get; set; }

    // Only set for local email/password accounts.
    [MaxLength(255)]
    public string? PasswordHash { get; set; }

    // Role is not stored in DB in option B.
    [NotMapped]
    public UserRole Role { get; set; } = UserRole.User;

    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }
}
