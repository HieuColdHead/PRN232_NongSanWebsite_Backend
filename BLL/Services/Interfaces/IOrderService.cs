using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IOrderService
{
    Task<PagedResult<OrderDto>> GetPagedAsync(int pageNumber, int pageSize);
    Task<PagedResult<OrderDto>> GetByUserIdAsync(Guid userId, int pageNumber, int pageSize);
    Task<OrderDto?> GetByIdAsync(int id);
    Task<OrderDto> CreateAsync(CreateOrderRequest request);
    Task UpdateAsync(int id, UpdateOrderRequest request);
    Task DeleteAsync(int id);
}
