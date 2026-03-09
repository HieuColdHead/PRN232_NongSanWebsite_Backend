namespace BLL.DTOs;

public class ExternalArticleDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? SourceName { get; set; }
}
