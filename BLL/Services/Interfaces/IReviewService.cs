using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IReviewService
{
    Task<PagedResult<ReviewDto>> GetPagedAsync(int pageNumber, int pageSize);
    Task<ReviewDto?> GetByIdAsync(int id);
    Task<PagedResult<ReviewDto>> GetByProductIdAsync(int productId, int pageNumber, int pageSize);
    Task<ReviewDto> CreateAsync(CreateReviewRequest request);
    Task UpdateAsync(int id, UpdateReviewRequest request);
    Task DeleteAsync(int id);
}
