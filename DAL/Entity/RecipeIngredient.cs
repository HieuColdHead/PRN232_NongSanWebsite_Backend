using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("RecipeIngredients")]
public class RecipeIngredient
{
    [Key]
    [Column("recipe_ingredient_id")]
    public Guid RecipeIngredientId { get; set; } = Guid.NewGuid();

    [Column("recipe_id")]
    public Guid RecipeId { get; set; }

    [ForeignKey("RecipeId")]
    public Recipe? Recipe { get; set; }

    [Column("product_id")]
    public Guid? ProductId { get; set; }

    [ForeignKey("ProductId")]
    public Product? Product { get; set; }

    [Required]
    [Column("ingredient_name")]
    [MaxLength(255)]
    public string IngredientName { get; set; } = string.Empty;

    [Column("quantity")]
    public decimal Quantity { get; set; }

    [Column("unit")]
    [MaxLength(50)]
    public string? Unit { get; set; }
}
