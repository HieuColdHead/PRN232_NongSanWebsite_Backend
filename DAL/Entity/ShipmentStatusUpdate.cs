using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("ShipmentStatusUpdates")]
public class ShipmentStatusUpdate
{
    [Key]
    [Column("status_update_id")]
    public Guid StatusUpdateId { get; set; } = Guid.NewGuid();

    [Column("shipment_id")]
    public Guid ShipmentId { get; set; }

    [ForeignKey(nameof(ShipmentId))]
    public Shipment? Shipment { get; set; }

    [Column("previous_status")]
    [MaxLength(50)]
    public string? PreviousStatus { get; set; }

    [Column("new_status")]
    [MaxLength(50)]
    public string? NewStatus { get; set; }

    [Column("raw_status")]
    [MaxLength(100)]
    public string? RawStatus { get; set; }

    [Column("note")]
    [MaxLength(500)]
    public string? Note { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;
}
