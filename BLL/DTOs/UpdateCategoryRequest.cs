using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public class UpdateCategoryRequest
{
    [MaxLength(255)]
    public string? Name { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public Guid? ParentId { get; set; }

    public List<Guid>? Children { get; set; }
}
