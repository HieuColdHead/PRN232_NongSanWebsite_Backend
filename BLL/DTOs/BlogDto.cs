namespace BLL.DTOs;

public class BlogDto
{
    public int BlogId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid AuthorId { get; set; }
    public string? AuthorName { get; set; }
}

public class CreateBlogRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? ThumbnailUrl { get; set; }
    public Guid AuthorId { get; set; }
}

public class UpdateBlogRequest
{
    public int BlogId { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Status { get; set; }
}
