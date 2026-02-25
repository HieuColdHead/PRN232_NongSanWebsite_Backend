using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;

namespace BLL.Services;

public class NotificationService : INotificationService
{
    private readonly IGenericRepository<Notification> _repository;

    public NotificationService(IGenericRepository<Notification> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<NotificationDto>> GetByUserIdAsync(Guid userId, int pageNumber, int pageSize)
    {
        var all = await _repository.FindAsync(n => n.UserId == userId);
        var totalCount = all.Count();
        var items = all
            .OrderByDescending(n => n.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);

        return new PagedResult<NotificationDto>
        {
            Items = items.Select(MapToDto),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<NotificationDto?> GetByIdAsync(int id)
    {
        var notification = await _repository.GetByIdAsync(id);
        if (notification == null) return null;
        return MapToDto(notification);
    }

    public async Task<NotificationDto> CreateAsync(CreateNotificationRequest request)
    {
        var notification = new Notification
        {
            Title = request.Title,
            Content = request.Content,
            Type = request.Type,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };

        await _repository.AddAsync(notification);
        await _repository.SaveChangesAsync();

        return MapToDto(notification);
    }

    public async Task MarkAsReadAsync(int id)
    {
        var notification = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Notification {id} not found");

        notification.IsRead = true;
        await _repository.UpdateAsync(notification);
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
    }

    private static NotificationDto MapToDto(Notification notification)
    {
        return new NotificationDto
        {
            NotificationId = notification.NotificationId,
            Title = notification.Title,
            Content = notification.Content,
            Type = notification.Type,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            UserId = notification.UserId
        };
    }
}
