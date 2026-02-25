using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetByUserIdAsync(Guid userId, int pageNumber, int pageSize);
    Task<NotificationDto?> GetByIdAsync(int id);
    Task<NotificationDto> CreateAsync(CreateNotificationRequest request);
    Task MarkAsReadAsync(int id);
    Task DeleteAsync(int id);
}
