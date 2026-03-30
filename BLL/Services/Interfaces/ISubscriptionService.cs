using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface ISubscriptionService
{
    Task<SubscriptionDto> SubscribeAsync(CreateSubscriptionRequest request);
    Task<IEnumerable<SubscriptionDto>> GetUserSubscriptionsAsync(Guid userId);
    Task<bool> CancelSubscriptionAsync(Guid id);
    Task<bool> UpdateNextDeliveryDateAsync(Guid id, DateTime nextDate);
}

public class SubscriptionDto
{
    public Guid SubscriptionId { get; set; }
    public Guid UserId { get; set; }
    public string Frequency { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime NextDeliveryDate { get; set; }
    public string Status { get; set; } = "Active";
    public string? ShippingAddress { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientPhone { get; set; }
    public List<SubscriptionItemDto> Items { get; set; } = new();
}

public class SubscriptionItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Unit { get; set; }
}

public class CreateSubscriptionRequest
{
    public Guid UserId { get; set; }
    public string Frequency { get; set; } = "Weekly";
    public string? ShippingAddress { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientPhone { get; set; }
    public List<SubscriptionItemRequest> Items { get; set; } = new();
}

public class SubscriptionItemRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
