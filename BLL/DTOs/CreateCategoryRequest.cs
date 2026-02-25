using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public class CreateCategoryRequest
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public List<string> Children { get; set; } = new List<string>();
}
