using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Data;
using DAL.Entity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using BLL.Hubs;

namespace BLL.Services;

public class ChatService : IChatService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<AppHub> _hubContext;
    private readonly INotificationService _notificationService;
    private readonly HashSet<string> _supportEmails;

    public ChatService(ApplicationDbContext context, IHubContext<AppHub> hubContext, IConfiguration configuration, INotificationService notificationService)
    {
        _context = context;
        _hubContext = hubContext;
        _notificationService = notificationService;

        var adminEmails = configuration.GetSection("AdminEmails").Get<string[]>() ?? Array.Empty<string>();
        var staffEmails = configuration.GetSection("StaffEmails").Get<string[]>() ?? Array.Empty<string>();

        _supportEmails = adminEmails.Concat(staffEmails)
            .Select(e => e.Trim().ToLowerInvariant())
            .ToHashSet();
    }

    public async Task<ChatMessageDto> SendMessageAsync(Guid senderId, SendMessageRequest request)
    {
        var sender = await _context.Users.FindAsync(senderId) 
            ?? throw new KeyNotFoundException("Sender not found.");

        var text = (request.Message ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("Nội dung tin nhắn không được rỗng.");

        Guid? receiverId = request.ReceiverId;

        var message = new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = text,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        var dto = new ChatMessageDto
        {
            Id = message.Id,
            SenderId = message.SenderId,
            SenderDisplayName = sender.DisplayName,
            ReceiverId = message.ReceiverId,
            Message = message.Content,
            SentAt = message.Timestamp,
            IsRead = false
        };

        // Emit via SignalR
        if (receiverId.HasValue)
        {
            var connectionId = AppHub.GetConnectionId(receiverId.Value);
            if (connectionId != null)
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveMessage", dto);
            }

            // If the sender is a support staff/admin, also broadcast to support groups
            // so other staff see the reply in real-time
            if (_supportEmails.Contains(sender.Email?.ToLower() ?? ""))
            {
                await _hubContext.Clients.Group("Admin").SendAsync("ReceiveMessage", dto);
                await _hubContext.Clients.Group("Staff").SendAsync("ReceiveMessage", dto);
            }

            // Create notification for receiver
            await _notificationService.CreateAsync(new CreateNotificationRequest
            {
                UserId = receiverId.Value,
                Title = $"Tin nhắn mới từ {sender.DisplayName}",
                Content = request.Message,
                Type = "ChatMessage"
            });
        }
        else
        {
            // Broadcast to Support groups (Customer -> Support)
            await _hubContext.Clients.Group("Admin").SendAsync("ReceiveMessage", dto);
            await _hubContext.Clients.Group("Staff").SendAsync("ReceiveMessage", dto);

            // Note: For Support, we might not want to spam notifications table for every message,
            // but the SignalR broadcast 'ReceiveMessage' will handle the UI update.
        }

        return dto;
    }

    public async Task<IEnumerable<ChatMessageDto>> GetChatHistoryAsync(Guid userId1, Guid userId2)
    {
        var messages = await _context.ChatMessages
            .Include(m => m.Sender)
            .Where(m => (m.SenderId == userId1 && (m.ReceiverId == userId2 || m.ReceiverId == null)) || 
                        (m.SenderId == userId2 && (m.ReceiverId == userId1 || m.ReceiverId == null)))
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return messages.Select(m => new ChatMessageDto
        {
            Id = m.Id,
            SenderId = m.SenderId,
            SenderDisplayName = m.Sender?.DisplayName,
            ReceiverId = m.ReceiverId,
            Message = m.Content,
            SentAt = m.Timestamp,
            IsRead = false
        });
    }

    public async Task<IEnumerable<ChatMessageDto>> GetMyChatHistoryAsync(Guid userId)
    {
        var messages = await _context.ChatMessages
            .Include(m => m.Sender)
            .Where(m => m.SenderId == userId || m.ReceiverId == userId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return messages.Select(m => new ChatMessageDto
        {
            Id = m.Id,
            SenderId = m.SenderId,
            SenderDisplayName = m.Sender?.DisplayName,
            ReceiverId = m.ReceiverId,
            Message = m.Content,
            SentAt = m.Timestamp,
            IsRead = false
        });
    }

    public async Task<IEnumerable<RecentChatDto>> GetRecentChatsForAdminAsync()
    {
        // Bảng DB không có is_read — không track đã đọc; badge unread = 0 (hoặc sau này thêm cột / bảng phụ).
        var messages = await _context.ChatMessages
            .Include(m => m.Sender)
            .Where(m => m.ReceiverId == null || (m.Sender != null && m.Sender.Email != null && _supportEmails.Contains(m.Sender.Email.ToLower())))
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync();

        var recentChats = messages
            .GroupBy(m =>
            {
                var senderEmail = m.Sender?.Email?.Trim().ToLowerInvariant();
                var senderIsSupport = senderEmail != null && _supportEmails.Contains(senderEmail);
                return senderIsSupport ? m.ReceiverId : m.SenderId;
            })
            .Select(g =>
            {
                var userId = g.Key;
                var last = g.OrderByDescending(x => x.Timestamp).FirstOrDefault();
                return new { UserId = userId, Last = last, Unread = 0 };
            })
            .Where(x => x.UserId.HasValue)
            .ToList();

        var userIds = recentChats.Select(x => x.UserId!.Value).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u);

        return recentChats
            .Select(x =>
            {
                users.TryGetValue(x.UserId!.Value, out var user);
                return new RecentChatDto
                {
                    UserId = x.UserId!.Value,
                    DisplayName = user?.DisplayName,
                    LastMessage = x.Last?.Content,
                    LastMessageTime = x.Last?.Timestamp ?? DateTime.MinValue,
                    UnreadCount = x.Unread
                };
            })
            .OrderByDescending(r => r.LastMessageTime);
    }

    public Task MarkAsReadAsync(Guid userId, Guid senderId)
    {
        // Không có cột is_read trên ChatMessages — giữ API để FE không lỗi; không cập nhật DB.
        return Task.CompletedTask;
    }
}
