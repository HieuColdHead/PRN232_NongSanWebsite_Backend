namespace BLL.DTOs;

public class ReviewDto
{
    public int ReviewId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Status { get; set; }
    public Guid UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
}

public class CreateReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public Guid UserId { get; set; }
    public int ProductId { get; set; }
}

public class UpdateReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? Status { get; set; }
}
