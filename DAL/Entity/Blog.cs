using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entity;

[Table("Blogs")]
public class Blog
{
    [Key]
    [Column("blog_id")]
    public int BlogId { get; set; }

    [Required]
    [Column("title")]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Column("content")]
    public string? Content { get; set; }

    [Column("thumbnail_url")]
    [MaxLength(500)]
    public string? ThumbnailUrl { get; set; }

    [Column("status")]
    [MaxLength(50)]
    public string? Status { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("author_id")]
    public Guid AuthorId { get; set; }

    [ForeignKey("AuthorId")]
    public User? Author { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;
}
