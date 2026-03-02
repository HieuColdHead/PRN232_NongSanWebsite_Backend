using System;
using System.Collections.Generic;

namespace BLL.DTOs;

public class ProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Origin { get; set; }
    public string? Unit { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsOrganic { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Guid? ProviderId { get; set; }
    public string? ProviderName { get; set; }
    public bool IsDeleted { get; set; }
    public List<ProductImageDto> ProductImages { get; set; } = new();
}

public class ProductImageDto
{
    public Guid ImageId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public Guid ProductId { get; set; }
}
