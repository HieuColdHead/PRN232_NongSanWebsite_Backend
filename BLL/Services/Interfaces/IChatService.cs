using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IChatService
{
    Task<ChatMessageDto> SendMessageAsync(Guid senderId, SendMessageRequest request);
    Task<IEnumerable<ChatMessageDto>> GetChatHistoryAsync(Guid userId1, Guid userId2);
    Task<IEnumerable<RecentChatDto>> GetRecentChatsForAdminAsync();
    Task MarkAsReadAsync(Guid userId, Guid senderId);
}
