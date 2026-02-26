namespace BLL.DTOs;

public class OrderDto
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public string? ShippingAddress { get; set; }
    public string? Status { get; set; }
    public string? VnPayStatus { get; set; }
    public Guid UserId { get; set; }
    public List<OrderDetailDto> OrderDetails { get; set; } = new();
}

public class CreateOrderRequest
{
    public decimal ShippingFee { get; set; }
    public string? ShippingAddress { get; set; }
    public Guid UserId { get; set; }
    public List<CreateOrderDetailRequest> OrderDetails { get; set; } = new();
}

public class UpdateOrderRequest
{
    public string? ShippingAddress { get; set; }
    public string? Status { get; set; }
    public string? VnPayStatus { get; set; }
}
