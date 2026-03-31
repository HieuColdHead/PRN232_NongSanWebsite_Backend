using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;

namespace BLL.Services;

public class RecipeService : IRecipeService
{
    private readonly IGenericRepository<Recipe> _recipeRepository;
    private readonly ICartService _cartService;
    private readonly IProductService _productService;

    public RecipeService(
        IGenericRepository<Recipe> recipeRepository,
        ICartService cartService,
        IProductService productService)
    {
        _recipeRepository = recipeRepository;
        _cartService = cartService;
        _productService = productService;
    }

    public async Task<IEnumerable<RecipeDto>> GetAllAsync()
    {
        var recipes = await _recipeRepository.GetAllAsync();
        return recipes.Select(MapToDto);
    }

    public async Task<RecipeDto?> GetByIdAsync(Guid id)
    {
        var recipe = await _recipeRepository.GetByIdAsync(id);
        return recipe != null ? MapToDto(recipe) : null;
    }

    public async Task<RecipeDto> CreateAsync(CreateRecipeRequest request)
    {
        var recipe = new Recipe
        {
            RecipeId = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Instructions = request.Instructions,
            ImageUrl = request.ImageUrl,
            CookingTimeMinutes = request.CookingTimeMinutes,
            Servings = request.Servings,
            CreatedAt = DateTime.UtcNow,
            Ingredients = request.Ingredients.Select(i => new RecipeIngredient
            {
                RecipeIngredientId = Guid.NewGuid(),
                ProductId = i.ProductId,
                IngredientName = i.IngredientName,
                Quantity = i.Quantity,
                Unit = i.Unit
            }).ToList()
        };

        await _recipeRepository.AddAsync(recipe);
        await _recipeRepository.SaveChangesAsync();
        return MapToDto(recipe);
    }

    public async Task UpdateAsync(Guid id, UpdateRecipeRequest request)
    {
        var recipe = await _recipeRepository.GetByIdAsync(id);
        if (recipe == null) throw new KeyNotFoundException($"Recipe {id} not found");

        if (request.Title != null) recipe.Title = request.Title;
        if (request.Description != null) recipe.Description = request.Description;
        if (request.Instructions != null) recipe.Instructions = request.Instructions;
        if (request.ImageUrl != null) recipe.ImageUrl = request.ImageUrl;
        if (request.CookingTimeMinutes.HasValue) recipe.CookingTimeMinutes = request.CookingTimeMinutes.Value;
        if (request.Servings.HasValue) recipe.Servings = request.Servings.Value;

        // Replace-all ingredients if provided in request.
        if (request.Ingredients != null)
        {
            recipe.Ingredients = request.Ingredients.Select(i => new RecipeIngredient
            {
                RecipeIngredientId = Guid.NewGuid(),
                RecipeId = recipe.RecipeId,
                ProductId = i.ProductId,
                IngredientName = i.IngredientName,
                Quantity = i.Quantity,
                Unit = i.Unit
            }).ToList();
        }

        await _recipeRepository.UpdateAsync(recipe);
        await _recipeRepository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _recipeRepository.DeleteAsync(id);
        await _recipeRepository.SaveChangesAsync();
    }

    public async Task<bool> AddAllIngredientsToCartAsync(Guid userId, Guid recipeId)
    {
        var recipe = await _recipeRepository.GetByIdAsync(recipeId);
        if (recipe == null) return false;

        var itemsToAdd = new List<AddCartItemRequest>();

        foreach (var ingredient in recipe.Ingredients)
        {
            if (ingredient.ProductId.HasValue)
            {
                // Rounding up to the nearest integer unit (e.g., 1 pack)
                var quantityToBuy = Math.Max(1, (int)Math.Ceiling(ingredient.Quantity));

                // Find the primary variant or first variant via ProductService
                var product = await _productService.GetByIdAsync(ingredient.ProductId.Value);

                var variant = product?.ProductVariants.FirstOrDefault();
                if (variant != null && variant.StockQuantity >= quantityToBuy)
                {
                    itemsToAdd.Add(new AddCartItemRequest
                    {
                        VariantId = variant.VariantId,
                        Quantity = quantityToBuy
                    });
                }
            }
        }

        if (itemsToAdd.Any())
        {
            await _cartService.AddItemsAsync(userId, itemsToAdd);
            return true;
        }

        return false;
    }

    private RecipeDto MapToDto(Recipe recipe)
    {
        return new RecipeDto
        {
            RecipeId = recipe.RecipeId,
            Title = recipe.Title,
            Description = recipe.Description,
            Instructions = recipe.Instructions,
            ImageUrl = recipe.ImageUrl,
            CookingTimeMinutes = recipe.CookingTimeMinutes,
            Servings = recipe.Servings,
            Ingredients = recipe.Ingredients.Select(i => new RecipeIngredientDto
            {
                ProductId = i.ProductId,
                IngredientName = i.IngredientName,
                Quantity = i.Quantity,
                Unit = i.Unit
            }).ToList()
        };
    }
}
