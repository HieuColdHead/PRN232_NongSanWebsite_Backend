using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("Shipments")]
public class Shipment
{
    [Key]
    [Column("shipment_id")]
    public Guid ShipmentId { get; set; } = Guid.NewGuid();

    [Column("order_id")]
    public Guid OrderId { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }

    [Column("ghn_order_code")]
    [MaxLength(100)]
    public string? GhnOrderCode { get; set; }

    [Column("service_id")]
    public int? ServiceId { get; set; }

    [Column("delivery_status")]
    [MaxLength(50)]
    public string? DeliveryStatus { get; set; }

    [Column("raw_status")]
    [MaxLength(100)]
    public string? RawStatus { get; set; }

    [Column("tracking_url")]
    [MaxLength(500)]
    public string? TrackingUrl { get; set; }

    [Column("shipping_fee")]
    public decimal ShippingFee { get; set; }

    [Column("cod_amount")]
    public decimal CodAmount { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    public ICollection<ShipmentStatusUpdate> StatusUpdates { get; set; } = new List<ShipmentStatusUpdate>();
}
