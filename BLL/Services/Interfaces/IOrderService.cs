using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IOrderService
{
    Task<PagedResult<OrderDto>> GetPagedAsync(int pageNumber, int pageSize);
    Task<PagedResult<OrderDto>> GetByUserIdAsync(Guid userId, int pageNumber, int pageSize);
    Task<OrderDto?> GetByIdAsync(Guid id);
    Task<OrderDto> CreateAsync(CreateOrderRequest request);
    Task<CheckoutPreviewDto> PreviewCheckoutAsync(Guid userId, CheckoutPreviewRequest request);
    Task<CheckoutOrderResultDto> CheckoutFromCartAsync(Guid userId, CheckoutOrderRequest request);
    Task UpdateAsync(Guid id, UpdateOrderRequest request);
    Task DeleteAsync(Guid id);
}
