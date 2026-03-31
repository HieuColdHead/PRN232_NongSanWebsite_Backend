using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IRecipeService
{
    Task<IEnumerable<RecipeDto>> GetAllAsync();
    Task<RecipeDto?> GetByIdAsync(Guid id);
    Task<RecipeDto> CreateAsync(CreateRecipeRequest request);
    Task UpdateAsync(Guid id, UpdateRecipeRequest request);
    Task DeleteAsync(Guid id);
    Task<bool> AddAllIngredientsToCartAsync(Guid userId, Guid recipeId);
}

public class RecipeDto
{
    public Guid RecipeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public string? ImageUrl { get; set; }
    public int CookingTimeMinutes { get; set; }
    public int Servings { get; set; }
    public List<RecipeIngredientDto> Ingredients { get; set; } = new();
}

public class RecipeIngredientDto
{
    public Guid? ProductId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
}

public class CreateRecipeRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public string? ImageUrl { get; set; }
    public int CookingTimeMinutes { get; set; }
    public int Servings { get; set; }
    public List<RecipeIngredientRequest> Ingredients { get; set; } = new();
}

public class RecipeIngredientRequest
{
    public Guid? ProductId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
}
