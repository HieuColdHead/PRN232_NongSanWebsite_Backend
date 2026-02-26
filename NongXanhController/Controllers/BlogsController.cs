using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
[Authorize]
public class BlogsController : BaseApiController
{
    private readonly IBlogService _service;

    public BlogsController(IBlogService service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<BlogDto>>>> GetBlogs([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        if (pageNumber < 1 || pageSize < 1)
        {
            return ErrorResponse<PagedResult<BlogDto>>("Page number and page size must be greater than 0.");
        }

        var result = await _service.GetPagedAsync(pageNumber, pageSize);
        return SuccessResponse(result);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<BlogDto>>> GetBlog(int id)
    {
        var blog = await _service.GetByIdAsync(id);

        if (blog == null)
        {
            return ErrorResponse<BlogDto>("Blog not found", statusCode: 404);
        }

        return SuccessResponse(blog);
    }

    [HttpGet("author/{authorId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<BlogDto>>>> GetByAuthor(Guid authorId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        if (pageNumber < 1 || pageSize < 1)
        {
            return ErrorResponse<PagedResult<BlogDto>>("Page number and page size must be greater than 0.");
        }

        var result = await _service.GetByAuthorIdAsync(authorId, pageNumber, pageSize);
        return SuccessResponse(result);
    }

    /// <summary>
    /// Get blogs written by the current user.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<PagedResult<BlogDto>>>> GetMyBlogs([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        if (pageNumber < 1 || pageSize < 1)
        {
            return ErrorResponse<PagedResult<BlogDto>>("Page number and page size must be greater than 0.");
        }

        var userId = GetCurrentUserId();
        if (userId is null) return ErrorResponse<PagedResult<BlogDto>>("Unauthorized", statusCode: 401);

        var result = await _service.GetByAuthorIdAsync(userId.Value, pageNumber, pageSize);
        return SuccessResponse(result);
    }

    /// <summary>
    /// AuthorId is automatically set from JWT token.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<BlogDto>>> PostBlog(CreateBlogRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return ErrorResponse<BlogDto>("Unauthorized", statusCode: 401);

        // Always assign AuthorId from JWT, ignore any value from request body
        request.AuthorId = userId.Value;

        var blog = await _service.CreateAsync(request);
        return SuccessResponse(blog, "Blog created successfully");
    }

    /// <summary>
    /// Author can update their own blog. Admin can update any blog.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> PutBlog(int id, UpdateBlogRequest request)
    {
        var existing = await _service.GetByIdAsync(id);
        if (existing == null)
        {
            return ErrorResponse<object>("Blog not found", statusCode: 404);
        }

        var userId = GetCurrentUserId();
        if (!IsAdmin() && existing.AuthorId != userId)
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.UpdateAsync(id, request);
        return SuccessResponse("Blog updated successfully");
    }

    /// <summary>
    /// Author can delete their own blog. Admin can delete any blog.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteBlog(int id)
    {
        var existing = await _service.GetByIdAsync(id);
        if (existing == null)
        {
            return ErrorResponse<object>("Blog not found", statusCode: 404);
        }

        var userId = GetCurrentUserId();
        if (!IsAdmin() && existing.AuthorId != userId)
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.DeleteAsync(id);
        return SuccessResponse("Blog deleted successfully");
    }
}
