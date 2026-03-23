namespace BLL.DTOs;

public class WishlistDto
{
    public Guid WishlistId { get; set; }
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public decimal ProductPrice { get; set; }
    public decimal? DiscountPrice { get; set; }
    public int Quantity { get; set; }
    public string? ProductImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
