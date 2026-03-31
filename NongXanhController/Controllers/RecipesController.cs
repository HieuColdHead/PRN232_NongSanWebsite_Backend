using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
public class RecipesController : BaseApiController
{
    private readonly IRecipeService _recipeService;

    public RecipesController(IRecipeService recipeService)
    {
        _recipeService = recipeService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<RecipeDto>>>> GetAll()
    {
        var result = await _recipeService.GetAllAsync();
        return SuccessResponse(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<RecipeDto>>> GetById(Guid id)
    {
        var result = await _recipeService.GetByIdAsync(id);
        if (result == null) return ErrorResponse<RecipeDto>("Recipe not found", statusCode: 404);
        return SuccessResponse(result);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<RecipeDto>>> Create([FromBody] CreateRecipeRequest request)
    {
        if (!IsAdminOrStaff())
        {
            return ErrorResponse<RecipeDto>("Forbidden", statusCode: 403);
        }

        var result = await _recipeService.CreateAsync(request);
        return SuccessResponse(result, "Recipe created successfully");
    }

    [Authorize]
    [HttpPost("{id}/add-to-cart")]
    public async Task<ActionResult<ApiResponse<bool>>> AddAllToCart(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _recipeService.AddAllIngredientsToCartAsync(userId.Value, id);
        if (!result) return ErrorResponse<bool>("Failed to add ingredients to cart");

        return SuccessResponse(true, "All available ingredients added to cart");
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> PutRecipe(Guid id, [FromBody] UpdateRecipeRequest request)
    {
        if (!IsAdminOrStaff())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _recipeService.UpdateAsync(id, request);
        return SuccessResponse("Recipe updated successfully");
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteRecipe(Guid id)
    {
        if (!IsAdminOrStaff())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _recipeService.DeleteAsync(id);
        return SuccessResponse("Recipe deleted successfully");
    }
}
