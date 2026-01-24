using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("Categories")]
public class Category
{
    [Key]
    [Column("category_id")]
    public int CategoryId { get; set; }

    [Required]
    [Column("category_name")]
    [MaxLength(255)]
    public string CategoryName { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    [Column("parent_category_id")]
    public int? ParentCategoryId { get; set; }

    [ForeignKey("ParentCategoryId")]
    public Category? ParentCategory { get; set; }

    [Column("status")]
    [MaxLength(50)]
    public string? Status { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;
}
