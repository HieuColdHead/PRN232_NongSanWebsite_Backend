using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Data;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BLL.Services;

public class ReviewService : IReviewService
{
    private static readonly string[] SuccessfulOrderStatuses = ["completed", "delivered", "success"];

    private readonly IGenericRepository<Review> _repository;
    private readonly ApplicationDbContext _context;

    public ReviewService(IGenericRepository<Review> repository, ApplicationDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    public async Task<PagedResult<ReviewDto>> GetPagedAsync(int pageNumber, int pageSize)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize);

        return new PagedResult<ReviewDto>
        {
            Items = items.Select(MapToDto),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<ReviewDto?> GetByIdAsync(Guid id)
    {
        var review = await _repository.GetByIdAsync(id);
        if (review == null) return null;
        return MapToDto(review);
    }

    public async Task<PagedResult<ReviewDto>> GetByProductIdAsync(Guid productId, int pageNumber, int pageSize)
    {
        var all = await _repository.FindAsync(r => r.ProductId == productId);
        var totalCount = all.Count();
        var items = all
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);

        return new PagedResult<ReviewDto>
        {
            Items = items.Select(MapToDto),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<ReviewDto> CreateAsync(CreateReviewRequest request)
    {
        if (request.Rating is < 1 or > 5)
        {
            throw new ArgumentException("Rating must be between 1 and 5.", nameof(request.Rating));
        }

        var existingReview = await _repository.FindAsync(r => r.UserId == request.UserId && r.ProductId == request.ProductId);
        if (existingReview.Any())
        {
            throw new InvalidOperationException("You have already reviewed this product.");
        }

        var hasSuccessfulOrder = await (
            from detail in _context.OrderDetails
            join order in _context.Orders on detail.OrderId equals order.OrderId
            join variant in _context.ProductVariants on detail.VariantId equals variant.VariantId
            where !order.IsDeleted
                  && order.UserId == request.UserId
                  && variant.ProductId == request.ProductId
                  && SuccessfulOrderStatuses.Contains((order.Status ?? string.Empty).ToLower())
            select detail.OrderDetailId
        ).AnyAsync();

        if (!hasSuccessfulOrder)
        {
            throw new InvalidOperationException("You can only review products from successful orders.");
        }

        var review = new Review
        {
            Rating = request.Rating,
            Comment = request.Comment,
            UserId = request.UserId,
            ProductId = request.ProductId,
            CreatedAt = DateTime.UtcNow,
            Status = "Active"
        };

        await _repository.AddAsync(review);
        await _repository.SaveChangesAsync();

        return MapToDto(review);
    }

    public async Task UpdateAsync(Guid id, UpdateReviewRequest request)
    {
        var review = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Review {id} not found");

        review.Rating = request.Rating;
        review.Comment = request.Comment;
        if (request.Status != null)
            review.Status = request.Status;

        await _repository.UpdateAsync(review);
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
    }

    private static ReviewDto MapToDto(Review review)
    {
        return new ReviewDto
        {
            ReviewId = review.ReviewId,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt,
            Status = review.Status,
            UserId = review.UserId,
            UserDisplayName = review.User?.DisplayName,
            ProductId = review.ProductId,
            ProductName = review.Product?.ProductName
        };
    }
}
