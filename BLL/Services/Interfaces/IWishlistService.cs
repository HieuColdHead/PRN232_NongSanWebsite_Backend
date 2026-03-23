using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IWishlistService
{
    Task<PagedResult<WishlistDto>> GetByUserIdAsync(Guid userId, int pageNumber, int pageSize);
    Task<WishlistDto> AddAsync(Guid userId, CreateWishlistRequest request);
    Task RemoveAsync(Guid userId, Guid productId);
}
