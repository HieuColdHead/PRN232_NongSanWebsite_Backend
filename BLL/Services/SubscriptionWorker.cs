using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BLL.Services.Interfaces;
using DAL.Data;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BLL.Services;

public class SubscriptionWorker : BackgroundService
{
    private readonly ILogger<SubscriptionWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public SubscriptionWorker(
        ILogger<SubscriptionWorker> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Subscription Worker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Subscription Worker is checking for due deliveries at: {time}", DateTimeOffset.Now);

            await ProcessSubscriptionsAsync();

            // Run once a day (or every hour for testing)
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task ProcessSubscriptionsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var subRepo = scope.ServiceProvider.GetRequiredService<IGenericRepository<Subscription>>();
        var orderRepo = scope.ServiceProvider.GetRequiredService<IGenericRepository<Order>>();
        var productRepo = scope.ServiceProvider.GetRequiredService<IGenericRepository<Product>>();

        var dueSubscriptions = await subRepo.FindAsync(s => 
            s.Status == "Active" && 
            !s.IsProcessing &&
            s.NextDeliveryDate <= DateTime.UtcNow);

        foreach (var sub in dueSubscriptions)
        {
            // Simple Distributed Lock using Atomic Update
            // Using Interpolated SQL for better parameter handling and using lowercase if needed (but keeping quotes to be sure)
            var affectedRows = await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Subscriptions\" SET \"is_processing\" = true WHERE \"subscription_id\" = {sub.SubscriptionId} AND \"is_processing\" = false");

            if (affectedRows == 0) continue; // Skip if another instance got it

            try
            {
                _logger.LogInformation("Processing due subscription {SubId}", sub.SubscriptionId);

                // Reload sub with items
                var currentSub = await context.Subscriptions
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(s => s.SubscriptionId == sub.SubscriptionId);
                
                if (currentSub == null)
                {
                    // If sub was deleted, release the lock and continue
                    await context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"Subscriptions\" SET \"is_processing\" = false WHERE \"subscription_id\" = {sub.SubscriptionId}");
                    continue;
                }

                var order = new Order
                {
                    OrderId = Guid.NewGuid(),
                    UserId = currentSub.UserId,
                    OrderDate = DateTime.UtcNow,
                    Status = "PendingPayment",
                    DeliveryStatus = "Processing",
                    RecipientName = currentSub.RecipientName,
                    RecipientPhone = currentSub.RecipientPhone,
                    ShippingAddress = currentSub.ShippingAddress
                };

                bool allItemsAvailable = true;
                foreach (var item in currentSub.Items)
                {
                    var product = await context.Products
                        .Include(p => p.ProductVariants)
                        .FirstOrDefaultAsync(p => p.ProductId == item.ProductId);

                    if (product == null || !product.ProductVariants.Any(v => v.StockQuantity >= item.Quantity))
                    {
                        _logger.LogWarning("Item {ProductId} for subscription {SubId} is out of stock.", item.ProductId, sub.SubscriptionId);
                        allItemsAvailable = false;
                        break; 
                    }

                    var variant = product.ProductVariants.First(v => v.StockQuantity >= item.Quantity);
                    var price = currentSub.PricingPolicy == "FixedPrice" && item.FixedPrice.HasValue 
                        ? item.FixedPrice.Value 
                        : (product.DiscountPrice ?? product.BasePrice);

                    order.OrderDetails.Add(new OrderDetail
                    {
                        OrderDetailId = Guid.NewGuid(),
                        VariantId = variant.VariantId,
                        Quantity = item.Quantity,
                        Price = price,
                        SubTotal = price * item.Quantity
                    });

                    // Deduct stock (simplified)
                    variant.StockQuantity -= item.Quantity;
                }

                if (!allItemsAvailable)
                {
                    _logger.LogWarning("Skipping subscription {SubId} due to stock issues.", sub.SubscriptionId);
                    currentSub.IsProcessing = false;
                    await context.SaveChangesAsync();
                    continue;
                }

                order.TotalAmount = order.OrderDetails.Sum(d => d.SubTotal);
                order.FinalAmount = order.TotalAmount;

                await context.Orders.AddAsync(order);

                // Update next delivery date
                currentSub.NextDeliveryDate = CalculateNextDate(currentSub.NextDeliveryDate, currentSub.Frequency);
                currentSub.LastProcessedDate = DateTime.UtcNow;
                currentSub.IsProcessing = false;

                await context.SaveChangesAsync();
                _logger.LogInformation("Successfully created order for subscription {SubId}", sub.SubscriptionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing subscription {SubId}", sub.SubscriptionId);
                // Ensure lock is released even on error using raw SQL for safety
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"Subscriptions\" SET \"is_processing\" = false WHERE \"subscription_id\" = {sub.SubscriptionId}");
            }
        }
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
}
