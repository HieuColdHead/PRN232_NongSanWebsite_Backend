using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public class ProductImageRequestDto
{
    [Required]
    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }
}
