using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("Recipes")]
public class Recipe
{
    [Key]
    [Column("recipe_id")]
    public Guid RecipeId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("title")]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("instructions")]
    public string? Instructions { get; set; }

    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("cooking_time_minutes")]
    public int CookingTimeMinutes { get; set; }

    [Column("servings")]
    public int Servings { get; set; }

    [Column("author_id")]
    public Guid? AuthorId { get; set; }

    [ForeignKey("AuthorId")]
    public User? Author { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
}
