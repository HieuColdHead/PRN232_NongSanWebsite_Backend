using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("Products")]
public class Product
{
    [Key]
    [Column("product_id")]
    public int ProductId { get; set; }

    [Required]
    [Column("product_name")]
    [MaxLength(255)]
    public string ProductName { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("origin")]
    [MaxLength(100)]
    public string? Origin { get; set; }

    [Column("unit")]
    [MaxLength(50)]
    public string? Unit { get; set; }

    [Column("base_price")]
    public decimal BasePrice { get; set; }

    [Column("is_organic")]
    public bool IsOrganic { get; set; }

    [Column("status")]
    [MaxLength(50)]
    public string? Status { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("category_id")]
    public int? CategoryId { get; set; }

    [ForeignKey("CategoryId")]
    public Category? Category { get; set; }

    [Column("provider_id")]
    public int? ProviderId { get; set; }

    [ForeignKey("ProviderId")]
    public Provider? Provider { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    public ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();
}
