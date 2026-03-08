using BLL.DTOs;
using BLL.DTOs.Ghn;

namespace BLL.Services.Interfaces;

public interface IShipmentService
{
    Task<ShipmentDto?> GetByOrderIdAsync(Guid orderId);
    Task<ShipmentDto?> CreateShipmentForOrderAsync(Guid orderId, string triggerSource, CancellationToken cancellationToken = default);
    Task<ShipmentDto> SyncShipmentStatusFromGhnAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task ProcessGhnWebhookAsync(GhnWebhookRequest request, string? tokenHeader, CancellationToken cancellationToken = default);
}
