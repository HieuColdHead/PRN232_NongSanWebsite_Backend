namespace BLL.DTOs;

public class OrderDetailDto
{
    public int OrderDetailId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal SubTotal { get; set; }
    public int OrderId { get; set; }
    public int VariantId { get; set; }
    public string? VariantName { get; set; }
}

public class CreateOrderDetailRequest
{
    public int VariantId { get; set; }
    public int Quantity { get; set; }
}
