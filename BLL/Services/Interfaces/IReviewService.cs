using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IReviewService
{
    Task<PagedResult<ReviewDto>> GetPagedAsync(int pageNumber, int pageSize);
    Task<ReviewDto?> GetByIdAsync(Guid id);
    Task<PagedResult<ReviewDto>> GetByProductIdAsync(Guid productId, int pageNumber, int pageSize);
    Task<ReviewDto> CreateAsync(CreateReviewRequest request);
    Task UpdateAsync(Guid id, UpdateReviewRequest request);
    Task DeleteAsync(Guid id);
}
