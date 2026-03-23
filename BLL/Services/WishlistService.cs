using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Data;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BLL.Services;

public class WishlistService : IWishlistService
{
    private readonly ApplicationDbContext _context;

    public WishlistService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<WishlistDto>> GetByUserIdAsync(Guid userId, int pageNumber, int pageSize)
    {
        var query = _context.Wishlists
            .Include(w => w.Product)
                .ThenInclude(p => p.ProductImages)
            .Include(w => w.Product)
                .ThenInclude(p => p.ProductVariants)
            .Where(w => w.UserId == userId);

        var totalCount = await query.CountAsync();
        
        var items = await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new WishlistDto
            {
                WishlistId = w.WishlistId,
                UserId = w.UserId,
                ProductId = w.ProductId,
                ProductName = w.Product!.ProductName,
                ProductPrice = w.Product.BasePrice,
                DiscountPrice = w.Product.DiscountPrice,
                Quantity = w.Product.ProductVariants.Sum(v => v.StockQuantity),
                ProductImageUrl = w.Product.ProductImages.FirstOrDefault()!.ImageUrl,
                CreatedAt = w.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<WishlistDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<WishlistDto> AddAsync(Guid userId, CreateWishlistRequest request)
    {
        var productExists = await _context.Products.AnyAsync(p => p.ProductId == request.ProductId && !p.IsDeleted);
        if (!productExists)
        {
            throw new KeyNotFoundException("Product not found or has been deleted.");
        }

        var existingWishlist = await _context.Wishlists
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == request.ProductId);

        if (existingWishlist != null)
        {
            throw new InvalidOperationException("Product is already in the wishlist.");
        }

        var wishlist = new Wishlist
        {
            UserId = userId,
            ProductId = request.ProductId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Wishlists.Add(wishlist);
        await _context.SaveChangesAsync();

        var addedProduct = await _context.Products
            .Include(p => p.ProductImages)
            .Include(p => p.ProductVariants)
            .FirstOrDefaultAsync(p => p.ProductId == request.ProductId);

        return new WishlistDto
        {
            WishlistId = wishlist.WishlistId,
            UserId = wishlist.UserId,
            ProductId = wishlist.ProductId,
            ProductName = addedProduct?.ProductName,
            ProductPrice = addedProduct?.BasePrice ?? 0,
            DiscountPrice = addedProduct?.DiscountPrice,
            Quantity = addedProduct?.ProductVariants?.Sum(v => v.StockQuantity) ?? 0,
            ProductImageUrl = addedProduct?.ProductImages?.FirstOrDefault()?.ImageUrl,
            CreatedAt = wishlist.CreatedAt
        };
    }

    public async Task RemoveAsync(Guid userId, Guid productId)
    {
        var wishlist = await _context.Wishlists
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

        if (wishlist == null)
        {
            throw new KeyNotFoundException("Wishlist item not found.");
        }

        _context.Wishlists.Remove(wishlist);
        await _context.SaveChangesAsync();
    }
}
