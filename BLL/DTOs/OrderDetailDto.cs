namespace BLL.DTOs;

public class OrderDetailDto
{
    public Guid OrderDetailId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal SubTotal { get; set; }
    public Guid OrderId { get; set; }
    public int VariantId { get; set; }
    public string? VariantName { get; set; }

    // Product information
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductImageUrl { get; set; }
    public string? Unit { get; set; }
    public string? Origin { get; set; }
}

public class CreateOrderDetailRequest
{
    public int VariantId { get; set; }
    public int Quantity { get; set; }
}
