using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;

namespace BLL.Services;

public class ProductVariantService : IProductVariantService
{
    private readonly IGenericRepository<ProductVariant> _repository;

    public ProductVariantService(IGenericRepository<ProductVariant> repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<ProductVariant>> GetAllAsync()
    {
        return _repository.GetAllAsync();
    }

    public Task<ProductVariant?> GetByIdAsync(int id)
    {
        return _repository.GetByIdAsync(id);
    }

    public async Task<ProductVariant> CreateAsync(CreateProductVariantRequest request)
    {
        var variant = new ProductVariant
        {
            VariantName = request.Name,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            Sku = request.Sku,
            Status = request.Status,
            ProductId = request.ProductId
        };

        await _repository.AddAsync(variant);
        await _repository.SaveChangesAsync();
        return variant;
    }

    public async Task UpdateAsync(ProductVariant productVariant)
    {
        await _repository.UpdateAsync(productVariant);
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
    }
}
