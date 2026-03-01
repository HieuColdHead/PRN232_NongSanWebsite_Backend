using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("Carts")]
public class Cart
{
    [Key]
    [Column("cart_id")]
    public Guid CartId { get; set; } = Guid.NewGuid();

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Column("status")]
    [MaxLength(50)]
    public string? Status { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}
