using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IBlogService
{
    Task<PagedResult<BlogDto>> GetPagedAsync(int pageNumber, int pageSize);
    Task<BlogDto?> GetByIdAsync(int id);
    Task<PagedResult<BlogDto>> GetByAuthorIdAsync(Guid authorId, int pageNumber, int pageSize);
    Task<BlogDto> CreateAsync(CreateBlogRequest request);
    Task UpdateAsync(int id, UpdateBlogRequest request);
    Task DeleteAsync(int id);
}
