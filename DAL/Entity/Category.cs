using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("Categories")]
public class Category
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("category_id")]
    public int CategoryId { get; set; }

    [Required]
    [Column("category_name")]
    [MaxLength(255)]
    public string CategoryName { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [NotMapped]
    public List<string> Children { get; set; } = new List<string>();
}
