using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public class ProductVariantRequestDto
{
    [Required]
    [MaxLength(255)]
    public string VariantName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal? DiscountPrice { get; set; }

    public int StockQuantity { get; set; }

    [MaxLength(100)]
    public string? Sku { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }
}
