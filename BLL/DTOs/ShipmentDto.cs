namespace BLL.DTOs;

public class ShipmentDto
{
    public Guid ShipmentId { get; set; }
    public Guid OrderId { get; set; }
    public string? GhnOrderCode { get; set; }
    public int? ServiceId { get; set; }
    public string? DeliveryStatus { get; set; }
    public string? RawStatus { get; set; }
    public string? TrackingUrl { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal CodAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ShipmentStatusUpdateDto
{
    public Guid StatusUpdateId { get; set; }
    public Guid ShipmentId { get; set; }
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? RawStatus { get; set; }
    public string? Note { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ShipmentBulkSyncResultDto
{
    public int TotalCandidates { get; set; }
    public int SyncedCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime SyncedAt { get; set; }
    public List<ShipmentBulkSyncFailureDto> Failures { get; set; } = new();
}

public class ShipmentBulkSyncFailureDto
{
    public Guid OrderId { get; set; }
    public string? GhnOrderCode { get; set; }
    public string Error { get; set; } = string.Empty;
}
