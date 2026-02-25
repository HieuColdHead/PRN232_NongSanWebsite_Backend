using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DAL.Entity;

[Table("OrderDetails")]
public class OrderDetail
{
    [Key]
    [Column("order_detail_id")]
    public int OrderDetailId { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("price")]
    public decimal Price { get; set; }

    [Column("sub_total")]
    public decimal SubTotal { get; set; }

    [Column("order_id")]
    public int OrderId { get; set; }

    [ForeignKey("OrderId")]
    [JsonIgnore]
    public Order? Order { get; set; }

    [Column("variant_id")]
    public int VariantId { get; set; }

    [ForeignKey("VariantId")]
    public ProductVariant? ProductVariant { get; set; }
}
