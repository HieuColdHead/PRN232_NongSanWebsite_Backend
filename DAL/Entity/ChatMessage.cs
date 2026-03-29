using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("ChatMessages")]
public class ChatMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SenderId { get; set; }

    [ForeignKey("SenderId")]
    public User? Sender { get; set; }

    public Guid? ReceiverId { get; set; }

    [ForeignKey("ReceiverId")]
    public User? Receiver { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; } = false;
}
