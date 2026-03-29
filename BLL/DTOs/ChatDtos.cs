namespace BLL.DTOs;

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public string? SenderDisplayName { get; set; }
    public Guid? ReceiverId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
}

public class SendMessageRequest
{
    public Guid? ReceiverId { get; set; } // Null if sending to Admin as a Customer
    public string Message { get; set; } = string.Empty;
}

public class RecentChatDto
{
    public Guid UserId { get; set; }
    public string? DisplayName { get; set; }
    public string? LastMessage { get; set; }
    public DateTime LastMessageTime { get; set; }
    public int UnreadCount { get; set; }
}
