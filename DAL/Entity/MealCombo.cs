using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("MealCombos")]
public class MealCombo
{
    [Key]
    [Column("meal_combo_id")]
    public Guid MealComboId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("name")]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("target_people_count")]
    public int TargetPeopleCount { get; set; } // 2, 4, 6

    [Column("duration_days")]
    public int DurationDays { get; set; } // 3, 7

    [Column("diet_type")]
    [MaxLength(50)]
    public string? DietType { get; set; } // Healthy, Eat Clean, Family

    [Column("base_price")]
    public decimal BasePrice { get; set; }

    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MealComboItem> Items { get; set; } = new List<MealComboItem>();
}
