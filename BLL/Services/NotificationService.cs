using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.SignalR;
using BLL.Hubs;
using DAL.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace BLL.Services;

public class NotificationService : INotificationService
{
    private readonly IGenericRepository<Notification> _repository;
    private readonly IHubContext<AppHub> _hubContext;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public NotificationService(
        IGenericRepository<Notification> repository, 
        IHubContext<AppHub> hubContext,
        ApplicationDbContext context,
        IConfiguration configuration)
    {
        _repository = repository;
        _hubContext = hubContext;
        _context = context;
        _configuration = configuration;
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

    public async Task<NotificationDto?> GetByIdAsync(Guid id)
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

        var dto = MapToDto(notification);

        // Push via SignalR
        var connectionId = AppHub.GetConnectionId(notification.UserId);
        if (connectionId != null)
        {
            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveNotification", dto);
        }

        return dto;
    }

    public async Task CreateForSupportAsync(CreateNotificationRequest request)
    {
        var adminEmails = _configuration.GetSection("AdminEmails").Get<string[]>() ?? Array.Empty<string>();
        var staffEmails = _configuration.GetSection("StaffEmails").Get<string[]>() ?? Array.Empty<string>();
        
        var supportEmails = adminEmails.Concat(staffEmails)
            .Select(e => e.Trim().ToLowerInvariant())
            .ToHashSet();

        // Get all support users in one query
        var supportUsers = await _context.Users
            .Where(u => u.Email != null && supportEmails.Contains(u.Email.ToLower()))
            .ToListAsync();

        foreach (var user in supportUsers)
        {
            var notification = new Notification
            {
                Title = request.Title,
                Content = request.Content,
                Type = request.Type,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            await _repository.AddAsync(notification);
        }

        await _repository.SaveChangesAsync();

        // Broadcast to support groups via SignalR
        var broadcastDto = new NotificationDto
        {
            Title = request.Title,
            Content = request.Content,
            Type = request.Type,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };

        await _hubContext.Clients.Group("Admin").SendAsync("ReceiveNotification", broadcastDto);
        await _hubContext.Clients.Group("Staff").SendAsync("ReceiveNotification", broadcastDto);
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var notification = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Notification {id} not found");

        notification.IsRead = true;
        await _repository.UpdateAsync(notification);
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
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
