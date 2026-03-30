namespace BLL.DTOs;

public class CartItemDto
{
    public Guid CartItemId { get; set; }
    public int Quantity { get; set; }
    public decimal PriceAtTime { get; set; }
    public decimal SubTotal { get; set; }
    public Guid CartId { get; set; }
    public Guid? VariantId { get; set; }
    public string? VariantName { get; set; }
    public Guid? MealComboId { get; set; }
    public string? MealComboName { get; set; }
    public string? ImageUrl { get; set; }
}
