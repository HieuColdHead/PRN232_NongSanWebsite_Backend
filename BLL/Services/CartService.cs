using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;

namespace BLL.Services;

public class CartService : ICartService
{
    private readonly IGenericRepository<Cart> _cartRepository;
    private readonly IGenericRepository<CartItem> _cartItemRepository;
    private readonly IGenericRepository<ProductVariant> _variantRepository;

    public CartService(
        IGenericRepository<Cart> cartRepository,
        IGenericRepository<CartItem> cartItemRepository,
        IGenericRepository<ProductVariant> variantRepository)
    {
        _cartRepository = cartRepository;
        _cartItemRepository = cartItemRepository;
        _variantRepository = variantRepository;
    }

    public async Task<CartDto?> GetByUserIdAsync(Guid userId)
    {
        var carts = await _cartRepository.FindAsync(c => c.UserId == userId && c.Status == "Active");
        var cart = carts.FirstOrDefault();
        if (cart == null) return null;

        return await MapToDto(cart);
    }

    public async Task<CartDto> AddItemAsync(Guid userId, AddCartItemRequest request)
    {
        var carts = await _cartRepository.FindAsync(c => c.UserId == userId && c.Status == "Active");
        var cart = carts.FirstOrDefault();

        if (cart == null)
        {
            cart = new Cart
            {
                UserId = userId,
                TotalAmount = 0,
                Status = "Active"
            };
            await _cartRepository.AddAsync(cart);
            await _cartRepository.SaveChangesAsync();
        }

        var variant = await _variantRepository.GetByIdAsync(request.VariantId);
        var price = variant?.Price ?? 0;

        // Check if item already exists in cart
        var existingItems = await _cartItemRepository.FindAsync(
            ci => ci.CartId == cart.CartId && ci.VariantId == request.VariantId);
        var existingItem = existingItems.FirstOrDefault();

        if (existingItem != null)
        {
            existingItem.Quantity += request.Quantity;
            existingItem.PriceAtTime = price;
            existingItem.SubTotal = price * existingItem.Quantity;
            await _cartItemRepository.UpdateAsync(existingItem);
        }
        else
        {
            var cartItem = new CartItem
            {
                CartId = cart.CartId,
                VariantId = request.VariantId,
                Quantity = request.Quantity,
                PriceAtTime = price,
                SubTotal = price * request.Quantity
            };
            await _cartItemRepository.AddAsync(cartItem);
        }

        await RecalculateCartTotal(cart);
        await _cartRepository.SaveChangesAsync();

        return await MapToDto(cart);
    }

    public async Task<CartDto> UpdateItemAsync(Guid userId, UpdateCartItemRequest request)
    {
        var carts = await _cartRepository.FindAsync(c => c.UserId == userId && c.Status == "Active");
        var cart = carts.FirstOrDefault()
            ?? throw new KeyNotFoundException("Cart not found");

        var item = await _cartItemRepository.GetByIdAsync(request.CartItemId)
            ?? throw new KeyNotFoundException($"Cart item {request.CartItemId} not found");

        if (item.CartId != cart.CartId)
            throw new InvalidOperationException("Cart item does not belong to this cart");

        item.Quantity = request.Quantity;
        item.SubTotal = item.PriceAtTime * request.Quantity;
        await _cartItemRepository.UpdateAsync(item);

        await RecalculateCartTotal(cart);
        await _cartRepository.SaveChangesAsync();

        return await MapToDto(cart);
    }

    public async Task RemoveItemAsync(Guid userId, Guid cartItemId)
    {
        var carts = await _cartRepository.FindAsync(c => c.UserId == userId && c.Status == "Active");
        var cart = carts.FirstOrDefault()
            ?? throw new KeyNotFoundException("Cart not found");

        var item = await _cartItemRepository.GetByIdAsync(cartItemId)
            ?? throw new KeyNotFoundException($"Cart item {cartItemId} not found");

        if (item.CartId != cart.CartId)
            throw new InvalidOperationException("Cart item does not belong to this cart");

        await _cartItemRepository.DeleteAsync(cartItemId);

        await RecalculateCartTotal(cart);
        await _cartRepository.SaveChangesAsync();
    }

    public async Task ClearCartAsync(Guid userId)
    {
        var carts = await _cartRepository.FindAsync(c => c.UserId == userId && c.Status == "Active");
        var cart = carts.FirstOrDefault();
        if (cart == null) return;

        var items = await _cartItemRepository.FindAsync(ci => ci.CartId == cart.CartId);
        foreach (var item in items)
        {
            await _cartItemRepository.DeleteAsync(item.CartItemId);
        }

        cart.TotalAmount = 0;
        await _cartRepository.UpdateAsync(cart);
        await _cartRepository.SaveChangesAsync();
    }

    private async Task RecalculateCartTotal(Cart cart)
    {
        var items = await _cartItemRepository.FindAsync(ci => ci.CartId == cart.CartId);
        cart.TotalAmount = items.Sum(i => i.SubTotal);
        await _cartRepository.UpdateAsync(cart);
    }

    private async Task<CartDto> MapToDto(Cart cart)
    {
        var items = await _cartItemRepository.FindAsync(ci => ci.CartId == cart.CartId);

        return new CartDto
        {
            CartId = cart.CartId,
            TotalAmount = cart.TotalAmount,
            Status = cart.Status,
            UserId = cart.UserId,
            CartItems = items.Select(i => new CartItemDto
            {
                CartItemId = i.CartItemId,
                Quantity = i.Quantity,
                PriceAtTime = i.PriceAtTime,
                SubTotal = i.SubTotal,
                CartId = i.CartId,
                VariantId = i.VariantId,
                VariantName = i.ProductVariant?.VariantName
            }).ToList()
        };
    }
}
