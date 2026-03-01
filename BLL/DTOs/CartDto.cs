namespace BLL.DTOs;

public class CartDto
{
    public Guid CartId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Status { get; set; }
    public Guid UserId { get; set; }
    public List<CartItemDto> CartItems { get; set; } = new();
}

public class AddCartItemRequest
{
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
}

public class UpdateCartItemRequest
{
    public Guid CartItemId { get; set; }
    public int Quantity { get; set; }
}
