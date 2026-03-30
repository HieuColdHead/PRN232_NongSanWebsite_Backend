using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("SubscriptionItems")]
public class SubscriptionItem
{
    [Key]
    [Column("subscription_item_id")]
    public Guid SubscriptionItemId { get; set; } = Guid.NewGuid();

    [Column("subscription_id")]
    public Guid SubscriptionId { get; set; }

    [ForeignKey("SubscriptionId")]
    public Subscription? Subscription { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [ForeignKey("ProductId")]
    public Product? Product { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("fixed_price")]
    public decimal? FixedPrice { get; set; }

    [Column("unit")]
    [MaxLength(50)]
    public string? Unit { get; set; }
}
