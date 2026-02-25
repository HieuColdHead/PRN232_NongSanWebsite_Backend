using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;

namespace BLL.Services;

public class OrderService : IOrderService
{
    private readonly IGenericRepository<Order> _orderRepository;
    private readonly IGenericRepository<OrderDetail> _orderDetailRepository;
    private readonly IGenericRepository<ProductVariant> _variantRepository;

    public OrderService(
        IGenericRepository<Order> orderRepository,
        IGenericRepository<OrderDetail> orderDetailRepository,
        IGenericRepository<ProductVariant> variantRepository)
    {
        _orderRepository = orderRepository;
        _orderDetailRepository = orderDetailRepository;
        _variantRepository = variantRepository;
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

    public async Task<OrderDto?> GetByIdAsync(int id)
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

    public async Task UpdateAsync(UpdateOrderRequest request)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId)
            ?? throw new KeyNotFoundException($"Order {request.OrderId} not found");

        if (request.ShippingAddress != null)
            order.ShippingAddress = request.ShippingAddress;
        if (request.Status != null)
            order.Status = request.Status;
        if (request.VnPayStatus != null)
            order.VnPayStatus = request.VnPayStatus;

        await _orderRepository.UpdateAsync(order);
        await _orderRepository.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _orderRepository.DeleteAsync(id);
        await _orderRepository.SaveChangesAsync();
    }

    private async Task<OrderDto> MapToDto(Order order)
    {
        var orderDetails = await _orderDetailRepository.FindAsync(d => d.OrderId == order.OrderId);

        return new OrderDto
        {
            OrderId = order.OrderId,
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
                VariantName = d.ProductVariant?.VariantName
            }).ToList()
        };
    }
}
