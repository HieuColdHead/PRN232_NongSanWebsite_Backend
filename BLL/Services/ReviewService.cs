using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;

namespace BLL.Services;

public class ReviewService : IReviewService
{
    private readonly IGenericRepository<Review> _repository;

    public ReviewService(IGenericRepository<Review> repository)
    {
        _repository = repository;
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

    public async Task<PagedResult<ReviewDto>> GetByProductIdAsync(int productId, int pageNumber, int pageSize)
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
