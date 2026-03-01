using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetByUserIdAsync(Guid userId, int pageNumber, int pageSize);
    Task<NotificationDto?> GetByIdAsync(Guid id);
    Task<NotificationDto> CreateAsync(CreateNotificationRequest request);
    Task MarkAsReadAsync(Guid id);
    Task DeleteAsync(Guid id);
}
