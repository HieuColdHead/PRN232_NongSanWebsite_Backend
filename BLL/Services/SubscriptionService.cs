using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;

namespace BLL.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IGenericRepository<Subscription> _subscriptionRepository;
    private readonly IGenericRepository<Product> _productRepository;

    public SubscriptionService(
        IGenericRepository<Subscription> subscriptionRepository,
        IGenericRepository<Product> productRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _productRepository = productRepository;
    }

    public async Task<SubscriptionDto> SubscribeAsync(CreateSubscriptionRequest request)
    {
        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid(),
            UserId = request.UserId,
            Frequency = request.Frequency,
            StartDate = DateTime.UtcNow,
            NextDeliveryDate = CalculateNextDate(DateTime.UtcNow, request.Frequency),
            Status = "Active",
            ShippingAddress = request.ShippingAddress,
            RecipientName = request.RecipientName,
            RecipientPhone = request.RecipientPhone,
            CreatedAt = DateTime.UtcNow,
            Items = request.Items.Select(i => new SubscriptionItem
            {
                SubscriptionItemId = Guid.NewGuid(),
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList()
        };

        await _subscriptionRepository.AddAsync(subscription);
        await _subscriptionRepository.SaveChangesAsync();
        return await MapToDtoAsync(subscription);
    }

    public async Task<IEnumerable<SubscriptionDto>> GetUserSubscriptionsAsync(Guid userId)
    {
        var subs = await _subscriptionRepository.FindAsync(s => s.UserId == userId);
        var dtos = new List<SubscriptionDto>();
        foreach (var sub in subs)
        {
            dtos.Add(await MapToDtoAsync(sub));
        }
        return dtos;
    }

    public async Task<bool> CancelSubscriptionAsync(Guid id)
    {
        var sub = await _subscriptionRepository.GetByIdAsync(id);
        if (sub == null) return false;
        
        sub.Status = "Cancelled";
        sub.UpdatedAt = DateTime.UtcNow;
        await _subscriptionRepository.UpdateAsync(sub);
        await _subscriptionRepository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateNextDeliveryDateAsync(Guid id, DateTime nextDate)
    {
        var sub = await _subscriptionRepository.GetByIdAsync(id);
        if (sub == null) return false;

        sub.NextDeliveryDate = nextDate;
        sub.UpdatedAt = DateTime.UtcNow;
        await _subscriptionRepository.UpdateAsync(sub);
        await _subscriptionRepository.SaveChangesAsync();
        return true;
    }

    private DateTime CalculateNextDate(DateTime current, string frequency)
    {
        return frequency switch
        {
            "Weekly" => current.AddDays(7),
            "BiWeekly" => current.AddDays(14),
            "Every3Days" => current.AddDays(3),
            _ => current.AddDays(7)
        };
    }

    private async Task<SubscriptionDto> MapToDtoAsync(Subscription sub)
    {
        var dto = new SubscriptionDto
        {
            SubscriptionId = sub.SubscriptionId,
            UserId = sub.UserId,
            Frequency = sub.Frequency,
            StartDate = sub.StartDate,
            NextDeliveryDate = sub.NextDeliveryDate,
            Status = sub.Status,
            ShippingAddress = sub.ShippingAddress,
            RecipientName = sub.RecipientName,
            RecipientPhone = sub.RecipientPhone
        };

        foreach (var item in sub.Items)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId);
            dto.Items.Add(new SubscriptionItemDto
            {
                ProductId = item.ProductId,
                ProductName = product?.ProductName ?? "Sản phẩm",
                Quantity = item.Quantity,
                Unit = product?.Unit ?? "kg"
            });
        }

        return dto;
    }
}
