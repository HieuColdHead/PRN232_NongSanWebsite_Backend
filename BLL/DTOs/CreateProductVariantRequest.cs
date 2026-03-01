using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public class CreateProductVariantRequest
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    [MaxLength(100)]
    public string? Sku { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }

    public Guid ProductId { get; set; }
}
