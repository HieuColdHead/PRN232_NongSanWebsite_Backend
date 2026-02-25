using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DAL.Entity;

[Table("CartItems")]
public class CartItem
{
    [Key]
    [Column("cart_item_id")]
    public int CartItemId { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("price_at_time")]
    public decimal PriceAtTime { get; set; }

    [Column("sub_total")]
    public decimal SubTotal { get; set; }

    [Column("cart_id")]
    public int CartId { get; set; }

    [ForeignKey("CartId")]
    [JsonIgnore]
    public Cart? Cart { get; set; }

    [Column("variant_id")]
    public int VariantId { get; set; }

    [ForeignKey("VariantId")]
    public ProductVariant? ProductVariant { get; set; }
}
