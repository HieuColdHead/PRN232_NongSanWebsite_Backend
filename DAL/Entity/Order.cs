using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("Orders")]
public class Order
{
    [Key]
    [Column("order_id")]
    public Guid OrderId { get; set; } = Guid.NewGuid();

    [Column("order_number")]
    [MaxLength(7)]
    public string OrderNumber { get; set; } = new Random().Next(1000000, 9999999).ToString();

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

    [Column("delivery_status")]
    [MaxLength(50)]
    public string? DeliveryStatus { get; set; }

    [Column("recipient_name")]
    [MaxLength(150)]
    public string? RecipientName { get; set; }

    [Column("recipient_phone")]
    [MaxLength(20)]
    public string? RecipientPhone { get; set; }

    [Column("province_code")]
    [MaxLength(20)]
    public string? ProvinceCode { get; set; }

    [Column("province_id")]
    public int? ProvinceId { get; set; }

    [Column("district_code")]
    [MaxLength(20)]
    public string? DistrictCode { get; set; }

    [Column("ward_code")]
    [MaxLength(20)]
    public string? WardCode { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    public Shipment? Shipment { get; set; }
}
