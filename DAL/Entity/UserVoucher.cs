using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("UserVouchers")]
public class UserVoucher
{
    [Key]
    [Column("user_voucher_id")]
    public int UserVoucherId { get; set; }

    [Column("is_used")]
    public bool IsUsed { get; set; } = false;

    [Column("used_at")]
    public DateTime? UsedAt { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [Column("voucher_id")]
    public int VoucherId { get; set; }

    [ForeignKey("VoucherId")]
    public Voucher? Voucher { get; set; }
}
