using BLL.DTOs;
using DAL.Entity;

namespace BLL.Services.Interfaces;

public interface IProductVariantService
{
    Task<IEnumerable<ProductVariant>> GetAllAsync();
    Task<ProductVariant?> GetByIdAsync(Guid id);
    Task<ProductVariant> CreateAsync(CreateProductVariantRequest request);
    Task UpdateAsync(Guid id, UpdateProductVariantRequest request);
    Task DeleteAsync(Guid id);
}
