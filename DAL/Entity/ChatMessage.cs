using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("ChatMessages")]
public class ChatMessage
{
    [Key]
    [Column("message_id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("sender_user_id")]
    public Guid SenderId { get; set; }

    [ForeignKey("SenderId")]
    public User? Sender { get; set; }

    [Column("receiver_user_id")]
    public Guid? ReceiverId { get; set; }

    [ForeignKey("ReceiverId")]
    public User? Receiver { get; set; }

    [Required]
    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Bảng Supabase chỉ có 6 cột — không có is_read.</summary>
    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;
}
