using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("Vouchers")]
public class Voucher
{
    [Key]
    [Column("voucher_id")]
    public Guid VoucherId { get; set; } = Guid.NewGuid();

    [Column("code")]
    [MaxLength(100)]
    public string? Code { get; set; }

    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    [Column("discount_type")]
    [MaxLength(50)]
    public string? DiscountType { get; set; }

    [Column("discount_value")]
    public decimal DiscountValue { get; set; }

    [Column("min_order_value")]
    public decimal MinOrderValue { get; set; }

    [Column("max_discount")]
    public decimal MaxDiscount { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("start_date")]
    public DateTime? StartDate { get; set; }

    [Column("end_date")]
    public DateTime? EndDate { get; set; }

    [Column("status")]
    [MaxLength(50)]
    public string? Status { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;
}
