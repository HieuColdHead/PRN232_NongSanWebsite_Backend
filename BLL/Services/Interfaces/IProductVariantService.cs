using BLL.DTOs;
using DAL.Entity;

namespace BLL.Services.Interfaces;

public interface IProductVariantService
{
    Task<IEnumerable<ProductVariant>> GetAllAsync();
    Task<ProductVariant?> GetByIdAsync(int id);
    Task<ProductVariant> CreateAsync(CreateProductVariantRequest request);
    Task UpdateAsync(ProductVariant productVariant);
    Task DeleteAsync(int id);
}
