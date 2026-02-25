using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("Orders")]
public class Order
{
    [Key]
    [Column("order_id")]
    public int OrderId { get; set; }

    [Column("order_date")]
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Column("shipping_fee")]
    public decimal ShippingFee { get; set; }

    [Column("discount_amount")]
    public decimal DiscountAmount { get; set; }

    [Column("final_amount")]
    public decimal FinalAmount { get; set; }

    [Column("shipping_address")]
    [MaxLength(500)]
    public string? ShippingAddress { get; set; }

    [Column("status")]
    [MaxLength(50)]
    public string? Status { get; set; }

    [Column("vnpay_status")]
    [MaxLength(50)]
    public string? VnPayStatus { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
