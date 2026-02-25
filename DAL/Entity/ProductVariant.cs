using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("ProductVariants")]
public class ProductVariant
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("variant_id")]
    public int VariantId { get; set; }

    [Required]
    [Column("variant_name")]
    [MaxLength(255)]
    public string VariantName { get; set; } = string.Empty;

    [Column("price")]
    public decimal Price { get; set; }

    [Column("stock_quantity")]
    public int StockQuantity { get; set; }

    [Column("sku")]
    [MaxLength(100)]
    public string? Sku { get; set; }

    [Column("status")]
    [MaxLength(50)]
    public string? Status { get; set; }

    [Column("product_id")]
    public int ProductId { get; set; }

    [ForeignKey("ProductId")]
    public Product? Product { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;
}
