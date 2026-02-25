using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("Payments")]
public class Payment
{
    [Key]
    [Column("payment_id")]
    public int PaymentId { get; set; }

    [Column("payment_method")]
    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    [Column("payment_status")]
    [MaxLength(50)]
    public string? PaymentStatus { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [Column("order_id")]
    public int OrderId { get; set; }

    [ForeignKey("OrderId")]
    public Order? Order { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;
}
