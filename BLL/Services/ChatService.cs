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

        Guid? receiverId = request.ReceiverId;
        
        // If receiver is null, it's a customer sending to admin
        if (receiverId == null)
        {
            // Find an admin to receive the message or just mark it as "to admin"
            // For now, let's look for any admin in the DB or just save it without receiverId to signify "Support Chat"
        }

        var message = new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = request.Message,
            Timestamp = DateTime.UtcNow,
            IsRead = false
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
            IsRead = message.IsRead
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
            IsRead = m.IsRead
        });
    }

    public async Task<IEnumerable<RecentChatDto>> GetRecentChatsForAdminAsync()
    {
        // Get all unique customers who have sent or received messages
        // This is simplified: grouped by sender (who is not a support member)
        var recentChats = await _context.ChatMessages
            .Include(m => m.Sender)
            .Where(m => m.ReceiverId == null || _supportEmails.Contains(m.Sender!.Email!.ToLower()))
            .GroupBy(m => m.SenderId != Guid.Empty && !_supportEmails.Contains(m.Sender!.Email!.ToLower()) ? m.SenderId : m.ReceiverId)
            .Select(g => new
            {
                UserId = g.Key,
                LastMessage = g.OrderByDescending(m => m.Timestamp).FirstOrDefault(),
                UnreadCount = g.Count(m => !m.IsRead && m.ReceiverId == null)
            })
            .ToListAsync();

        var results = new List<RecentChatDto>();
        foreach (var chat in recentChats)
        {
            if (chat.UserId == null) continue;
            
            var user = await _context.Users.FindAsync(chat.UserId);
            results.Add(new RecentChatDto
            {
                UserId = chat.UserId.Value,
                DisplayName = user?.DisplayName,
                LastMessage = chat.LastMessage?.Content,
                LastMessageTime = chat.LastMessage?.Timestamp ?? DateTime.MinValue,
                UnreadCount = chat.UnreadCount
            });
        }

        return results.OrderByDescending(r => r.LastMessageTime);
    }

    public async Task MarkAsReadAsync(Guid userId, Guid senderId)
    {
        var messages = await _context.ChatMessages
            .Where(m => m.ReceiverId == userId && m.SenderId == senderId && !m.IsRead)
            .ToListAsync();

        foreach (var m in messages)
        {
            m.IsRead = true;
        }

        await _context.SaveChangesAsync();
    }
}
