using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("MealComboItems")]
public class MealComboItem
{
    [Key]
    [Column("meal_combo_item_id")]
    public Guid MealComboItemId { get; set; } = Guid.NewGuid();

    [Column("meal_combo_id")]
    public Guid MealComboId { get; set; }

    [ForeignKey("MealComboId")]
    public MealCombo? MealCombo { get; set; }

    [Required]
    [Column("product_id")]
    public Guid ProductId { get; set; }

    [ForeignKey("ProductId")]
    public Product? Product { get; set; }

    [Column("quantity")]
    public decimal Quantity { get; set; }

    [Column("unit")]
    [MaxLength(50)]
    public string? Unit { get; set; }
}
