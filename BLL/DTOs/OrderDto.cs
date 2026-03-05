namespace BLL.DTOs;

public class OrderDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
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

public class CheckoutPreviewRequest
{
    public List<Guid> CartItemIds { get; set; } = new();
    public decimal ShippingFee { get; set; }
    public string? VoucherCode { get; set; }
}

public class CheckoutOrderRequest
{
    public List<Guid> CartItemIds { get; set; } = new();
    public decimal ShippingFee { get; set; }
    public string? ShippingAddress { get; set; }
    public string? ShippingMethod { get; set; }
    public string? PaymentMethod { get; set; }
    public string? VoucherCode { get; set; }
}

public class CheckoutItemDto
{
    public Guid CartItemId { get; set; }
    public Guid VariantId { get; set; }
    public string? ProductName { get; set; }
    public string? VariantName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
}

public class CheckoutPreviewDto
{
    public List<CheckoutItemDto> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public string? VoucherCode { get; set; }
}

public class CheckoutOrderResultDto
{
    public required OrderDto Order { get; set; }
    public required PaymentDto Payment { get; set; }
    public string? PaymentUrl { get; set; }
    public string? VnPayTransactionRef { get; set; }
}

public class UpdateOrderRequest
{
    public string? ShippingAddress { get; set; }
    public string? Status { get; set; }
    public string? VnPayStatus { get; set; }
}
