using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("Transactions")]
public class Transaction
{
    [Key]
    [Column("transaction_id")]
    public int TransactionId { get; set; }

    [Column("transaction_code")]
    [MaxLength(100)]
    public string? TransactionCode { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("transaction_date")]
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    [Column("gateway")]
    [MaxLength(50)]
    public string? Gateway { get; set; }

    [Column("status")]
    [MaxLength(50)]
    public string? Status { get; set; }

    [Column("payment_id")]
    public int PaymentId { get; set; }

    [ForeignKey("PaymentId")]
    public Payment? Payment { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;
}
