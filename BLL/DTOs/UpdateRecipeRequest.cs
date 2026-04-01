using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public class UpdateRecipeRequest
{
    [MaxLength(255)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? Instructions { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public int? CookingTimeMinutes { get; set; }

    public int? Servings { get; set; }

    public List<UpdateRecipeIngredientRequest>? Ingredients { get; set; }
}

public class UpdateRecipeIngredientRequest
{
    public Guid? ProductId { get; set; }

    public Guid? VariantId { get; set; }

    [Required]
    [MaxLength(255)]
    public string IngredientName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }
}
