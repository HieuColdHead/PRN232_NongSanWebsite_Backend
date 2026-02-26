using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public class UpdateProductVariantRequest
{
    [MaxLength(255)]
    public string? Name { get; set; }

    public decimal? Price { get; set; }

    public int? StockQuantity { get; set; }

    [MaxLength(100)]
    public string? Sku { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }
}
