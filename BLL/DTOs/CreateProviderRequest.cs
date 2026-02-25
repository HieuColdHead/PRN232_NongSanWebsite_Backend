using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public class CreateProviderRequest
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(255)]
    public string? Address { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(255)]
    public string? Email { get; set; }

    public decimal? RatingAverage { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }
}
