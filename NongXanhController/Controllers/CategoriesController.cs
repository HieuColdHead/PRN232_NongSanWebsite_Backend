using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController : BaseApiController
{
    private readonly ICategoryService _service;

    public CategoriesController(ICategoryService service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<CategoryDto>>>> GetCategories()
    {
        var categories = await _service.GetAllAsync();
        return SuccessResponse(categories);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> GetCategory(int id)
    {
        var category = await _service.GetByIdAsync(id);

        if (category == null)
        {
            return ErrorResponse<CategoryDto>("Category not found", statusCode: 404);
        }

        return SuccessResponse(category);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> PostCategory(CreateCategoryRequest request)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<CategoryDto>("Forbidden", statusCode: 403);
        }

        var category = await _service.CreateAsync(request);
        return SuccessResponse(category, "Category created successfully");
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> PutCategory(int id, UpdateCategoryRequest request)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.UpdateAsync(id, request);
        return SuccessResponse("Category updated successfully");
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCategory(int id)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.DeleteAsync(id);
        return SuccessResponse("Category deleted successfully");
    }
}
