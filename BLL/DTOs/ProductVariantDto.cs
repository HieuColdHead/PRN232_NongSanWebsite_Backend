namespace BLL.DTOs;

public class ProductVariantDto
{
    public Guid VariantId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string? Sku { get; set; }
    public string? Status { get; set; }
    public Guid ProductId { get; set; }
}
