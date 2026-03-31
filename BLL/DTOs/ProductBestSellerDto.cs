namespace BLL.DTOs;

public sealed class ProductBestSellerDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Unit { get; set; }
    public decimal SoldQuantity { get; set; }
}

