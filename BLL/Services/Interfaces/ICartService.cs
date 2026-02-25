using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface ICartService
{
    Task<CartDto?> GetByUserIdAsync(Guid userId);
    Task<CartDto> AddItemAsync(Guid userId, AddCartItemRequest request);
    Task<CartDto> UpdateItemAsync(Guid userId, UpdateCartItemRequest request);
    Task RemoveItemAsync(Guid userId, int cartItemId);
    Task ClearCartAsync(Guid userId);
}
