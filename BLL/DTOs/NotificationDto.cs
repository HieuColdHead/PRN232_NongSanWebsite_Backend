namespace BLL.DTOs;

public class NotificationDto
{
    public int NotificationId { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid UserId { get; set; }
}

public class CreateNotificationRequest
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Type { get; set; }
    public Guid UserId { get; set; }
}
