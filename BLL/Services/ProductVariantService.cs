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

    public async Task UpdateAsync(int id, UpdateProductVariantRequest request)
    {
        var variant = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"ProductVariant {id} not found");

        if (request.Name != null) variant.VariantName = request.Name;
        if (request.Price.HasValue) variant.Price = request.Price.Value;
        if (request.StockQuantity.HasValue) variant.StockQuantity = request.StockQuantity.Value;
        if (request.Sku != null) variant.Sku = request.Sku;
        if (request.Status != null) variant.Status = request.Status;

        await _repository.UpdateAsync(variant);
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
    }
}
