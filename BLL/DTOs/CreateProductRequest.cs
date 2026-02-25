using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public class CreateProductRequest
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Origin { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    public decimal BasePrice { get; set; }

    public bool IsOrganic { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }

    public int? CategoryId { get; set; }

    public int? ProviderId { get; set; }
}
