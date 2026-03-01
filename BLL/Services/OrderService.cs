using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Data;
using DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BLL.Services;

public class OrderService : IOrderService
{
    private readonly IGenericRepository<Order> _orderRepository;
    private readonly IGenericRepository<OrderDetail> _orderDetailRepository;
    private readonly IGenericRepository<ProductVariant> _variantRepository;
    private readonly ApplicationDbContext _context;

    public OrderService(
        IGenericRepository<Order> orderRepository,
        IGenericRepository<OrderDetail> orderDetailRepository,
        IGenericRepository<ProductVariant> variantRepository,
        ApplicationDbContext context)
    {
        _orderRepository = orderRepository;
        _orderDetailRepository = orderDetailRepository;
        _variantRepository = variantRepository;
        _context = context;
    }

    public async Task<PagedResult<OrderDto>> GetPagedAsync(int pageNumber, int pageSize)
    {
        var (items, totalCount) = await _orderRepository.GetPagedAsync(pageNumber, pageSize);

        var dtos = new List<OrderDto>();
        foreach (var order in items)
        {
            dtos.Add(await MapToDto(order));
        }

        return new PagedResult<OrderDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<OrderDto>> GetByUserIdAsync(Guid userId, int pageNumber, int pageSize)
    {
        var all = await _orderRepository.FindAsync(o => o.UserId == userId);
        var totalCount = all.Count();
        var items = all
            .OrderByDescending(o => o.OrderDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);

        var dtos = new List<OrderDto>();
        foreach (var order in items)
        {
            dtos.Add(await MapToDto(order));
        }

        return new PagedResult<OrderDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<OrderDto?> GetByIdAsync(Guid id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null) return null;
        return await MapToDto(order);
    }

    public async Task<OrderDto> CreateAsync(CreateOrderRequest request)
    {
        var order = new Order
        {
            OrderDate = DateTime.UtcNow,
            ShippingFee = request.ShippingFee,
            ShippingAddress = request.ShippingAddress,
            UserId = request.UserId,
            Status = "Pending",
            VnPayStatus = "Pending"
        };

        decimal totalAmount = 0;
        var details = new List<OrderDetail>();

        foreach (var item in request.OrderDetails)
        {
            var variant = await _variantRepository.GetByIdAsync(item.VariantId);
            var price = variant?.Price ?? 0;
            var subTotal = price * item.Quantity;
            totalAmount += subTotal;

            details.Add(new OrderDetail
            {
                VariantId = item.VariantId,
                Quantity = item.Quantity,
                Price = price,
                SubTotal = subTotal
            });
        }

        order.TotalAmount = totalAmount;
        order.DiscountAmount = 0;
        order.FinalAmount = totalAmount + request.ShippingFee - order.DiscountAmount;
        order.OrderDetails = details;

        await _orderRepository.AddAsync(order);
        await _orderRepository.SaveChangesAsync();

        return await MapToDto(order);
    }

    public async Task UpdateAsync(Guid id, UpdateOrderRequest request)
    {
        var order = await _orderRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Order {id} not found");

        if (request.ShippingAddress != null)
            order.ShippingAddress = request.ShippingAddress;
        if (request.Status != null)
            order.Status = request.Status;
        if (request.VnPayStatus != null)
            order.VnPayStatus = request.VnPayStatus;

        await _orderRepository.UpdateAsync(order);
        await _orderRepository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _orderRepository.DeleteAsync(id);
        await _orderRepository.SaveChangesAsync();
    }

    private async Task<OrderDto> MapToDto(Order order)
    {
        // Load order details with ProductVariant -> Product -> ProductImages
        var orderDetails = await _context.Set<OrderDetail>()
            .Where(d => d.OrderId == order.OrderId)
            .Include(d => d.ProductVariant!)
                .ThenInclude(v => v.Product!)
                    .ThenInclude(p => p.ProductImages)
            .ToListAsync();

        return new OrderDto
        {
            OrderId = order.OrderId,
            OrderNumber = order.OrderNumber,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            ShippingFee = order.ShippingFee,
            DiscountAmount = order.DiscountAmount,
            FinalAmount = order.FinalAmount,
            ShippingAddress = order.ShippingAddress,
            Status = order.Status,
            VnPayStatus = order.VnPayStatus,
            UserId = order.UserId,
            OrderDetails = orderDetails.Select(d => new OrderDetailDto
            {
                OrderDetailId = d.OrderDetailId,
                Quantity = d.Quantity,
                Price = d.Price,
                SubTotal = d.SubTotal,
                OrderId = d.OrderId,
                VariantId = d.VariantId,
                VariantName = d.ProductVariant?.VariantName,
                ProductId = d.ProductVariant?.ProductId ?? Guid.Empty,
                ProductName = d.ProductVariant?.Product?.ProductName,
                ProductImageUrl = d.ProductVariant?.Product?.ProductImages
                    ?.FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                    ?? d.ProductVariant?.Product?.ProductImages?.FirstOrDefault()?.ImageUrl,
                Unit = d.ProductVariant?.Product?.Unit,
                Origin = d.ProductVariant?.Product?.Origin
            }).ToList()
        };
    }
}
