using DAL.Entity;

namespace BLL.Services.Interfaces;

public interface IProductVariantService
{
    Task<IEnumerable<ProductVariant>> GetAllAsync();
    Task<ProductVariant?> GetByIdAsync(int id);
    Task AddAsync(ProductVariant productVariant);
    Task UpdateAsync(ProductVariant productVariant);
    Task DeleteAsync(int id);
}
