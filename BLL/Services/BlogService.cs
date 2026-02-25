using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;

namespace BLL.Services;

public class BlogService : IBlogService
{
    private readonly IGenericRepository<Blog> _repository;

    public BlogService(IGenericRepository<Blog> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<BlogDto>> GetPagedAsync(int pageNumber, int pageSize)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize);

        return new PagedResult<BlogDto>
        {
            Items = items.Select(MapToDto),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<BlogDto?> GetByIdAsync(int id)
    {
        var blog = await _repository.GetByIdAsync(id);
        if (blog == null) return null;
        return MapToDto(blog);
    }

    public async Task<PagedResult<BlogDto>> GetByAuthorIdAsync(Guid authorId, int pageNumber, int pageSize)
    {
        var all = await _repository.FindAsync(b => b.AuthorId == authorId);
        var totalCount = all.Count();
        var items = all
            .OrderByDescending(b => b.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);

        return new PagedResult<BlogDto>
        {
            Items = items.Select(MapToDto),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<BlogDto> CreateAsync(CreateBlogRequest request)
    {
        var blog = new Blog
        {
            Title = request.Title,
            Content = request.Content,
            ThumbnailUrl = request.ThumbnailUrl,
            AuthorId = request.AuthorId,
            CreatedAt = DateTime.UtcNow,
            Status = "Published"
        };

        await _repository.AddAsync(blog);
        await _repository.SaveChangesAsync();

        return MapToDto(blog);
    }

    public async Task UpdateAsync(UpdateBlogRequest request)
    {
        var blog = await _repository.GetByIdAsync(request.BlogId)
            ?? throw new KeyNotFoundException($"Blog {request.BlogId} not found");

        if (request.Title != null) blog.Title = request.Title;
        if (request.Content != null) blog.Content = request.Content;
        if (request.ThumbnailUrl != null) blog.ThumbnailUrl = request.ThumbnailUrl;
        if (request.Status != null) blog.Status = request.Status;
        blog.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(blog);
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
    }

    private static BlogDto MapToDto(Blog blog)
    {
        return new BlogDto
        {
            BlogId = blog.BlogId,
            Title = blog.Title,
            Content = blog.Content,
            ThumbnailUrl = blog.ThumbnailUrl,
            Status = blog.Status,
            CreatedAt = blog.CreatedAt,
            UpdatedAt = blog.UpdatedAt,
            AuthorId = blog.AuthorId,
            AuthorName = blog.Author?.DisplayName
        };
    }
}
