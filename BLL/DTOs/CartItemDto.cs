namespace BLL.DTOs;

public class CartItemDto
{
    public Guid CartItemId { get; set; }
    public int Quantity { get; set; }
    public decimal PriceAtTime { get; set; }
    public decimal SubTotal { get; set; }
    public Guid CartId { get; set; }
    public int VariantId { get; set; }
    public string? VariantName { get; set; }
}
