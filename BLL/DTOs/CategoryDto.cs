namespace BLL.DTOs;

public class CategoryDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public List<CategoryDto> Children { get; set; } = new();
}
