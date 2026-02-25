namespace BLL.DTOs;

public class CartDto
{
    public int CartId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Status { get; set; }
    public Guid UserId { get; set; }
    public List<CartItemDto> CartItems { get; set; } = new();
}

public class AddCartItemRequest
{
    public int VariantId { get; set; }
    public int Quantity { get; set; }
}

public class UpdateCartItemRequest
{
    public int CartItemId { get; set; }
    public int Quantity { get; set; }
}
