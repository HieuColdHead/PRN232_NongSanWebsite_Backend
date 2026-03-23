using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IWishlistService
{
    Task<PagedResult<WishlistDto>> GetByUserIdAsync(Guid userId, int pageNumber, int pageSize);
    Task<WishlistDto> AddAsync(CreateWishlistRequest request);
    Task RemoveAsync(Guid userId, Guid productId);
}
