using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("Subscriptions")]
public class Subscription
{
    [Key]
    [Column("subscription_id")]
    public Guid SubscriptionId { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [Column("frequency")]
    [MaxLength(50)]
    public string Frequency { get; set; } = string.Empty; // Weekly, BiWeekly, Every3Days

    [Column("start_date")]
    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    [Column("next_delivery_date")]
    public DateTime NextDeliveryDate { get; set; }

    [Column("status")]
    [MaxLength(50)]
    public string Status { get; set; } = "Active"; // Active, Paused, Cancelled

    [Column("shipping_address")]
    [MaxLength(500)]
    public string? ShippingAddress { get; set; }

    [Column("recipient_name")]
    [MaxLength(150)]
    public string? RecipientName { get; set; }

    [Column("recipient_phone")]
    [MaxLength(20)]
    public string? RecipientPhone { get; set; }

    [Column("is_processing")]
    public bool IsProcessing { get; set; } = false;

    [Column("pricing_policy")]
    [MaxLength(50)]
    public string PricingPolicy { get; set; } = "MarketPrice"; // MarketPrice, FixedPrice

    [Column("last_processed_date")]
    public DateTime? LastProcessedDate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public ICollection<SubscriptionItem> Items { get; set; } = new List<SubscriptionItem>();
}
