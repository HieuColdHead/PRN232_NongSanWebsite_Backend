using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("EmailOtps")]
public class EmailOtp
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    // Store hashed OTP only
    [Required]
    [MaxLength(128)]
    public string OtpHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ConsumedAt { get; set; }

    [MaxLength(50)]
    public string? Purpose { get; set; }

    public Guid? UserId { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }
}
