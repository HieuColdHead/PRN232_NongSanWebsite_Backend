using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IBlogService
{
    Task<PagedResult<BlogDto>> GetPagedAsync(int pageNumber, int pageSize);
    Task<BlogDto?> GetByIdAsync(Guid id);
    Task<PagedResult<BlogDto>> GetByAuthorIdAsync(Guid authorId, int pageNumber, int pageSize);
    Task<BlogDto> CreateAsync(CreateBlogRequest request);
    Task UpdateAsync(Guid id, UpdateBlogRequest request);
    Task DeleteAsync(Guid id);
}
