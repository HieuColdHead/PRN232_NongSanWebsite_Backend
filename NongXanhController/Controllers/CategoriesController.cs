using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class CategoriesController : BaseApiController
{
    private readonly ICategoryService _service;

    public CategoriesController(ICategoryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<Category>>>> GetCategories()
    {
        var categories = await _service.GetAllAsync();
        return SuccessResponse(categories);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<Category>>> GetCategory(int id)
    {
        var category = await _service.GetByIdAsync(id);

        if (category == null)
        {
            return ErrorResponse<Category>("Category not found", statusCode: 404);
        }

        return SuccessResponse(category);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Category>>> PostCategory(CreateCategoryRequest request)
    {
        var category = await _service.CreateAsync(request);
        return SuccessResponse(category, "Category created successfully");
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> PutCategory(int id, Category category)
    {
        if (id != category.CategoryId)
        {
            return ErrorResponse<object>("Category ID mismatch");
        }

        await _service.UpdateAsync(category);
        return SuccessResponse("Category updated successfully");
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCategory(int id)
    {
        await _service.DeleteAsync(id);
        return SuccessResponse("Category deleted successfully");
    }
}
