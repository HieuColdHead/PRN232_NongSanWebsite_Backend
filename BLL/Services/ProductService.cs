using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;

namespace BLL.Services;

public class ProductService : IProductService
{
    private readonly IGenericRepository<Product> _repository;

    public ProductService(IGenericRepository<Product> repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<Product>> GetAllAsync()
    {
        return _repository.GetAllAsync();
    }

    public async Task<PagedResult<Product>> GetPagedAsync(int pageNumber, int pageSize)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize);
        
        return new PagedResult<Product>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public Task<Product?> GetByIdAsync(int id)
    {
        return _repository.GetByIdAsync(id);
    }

    public async Task<Product> CreateAsync(CreateProductRequest request)
    {
        var product = new Product
        {
            ProductName = request.Name,
            Description = request.Description,
            Origin = request.Origin,
            Unit = request.Unit,
            BasePrice = request.BasePrice,
            IsOrganic = request.IsOrganic,
            Status = request.Status,
            CategoryId = request.CategoryId,
            ProviderId = request.ProviderId,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(product);
        await _repository.SaveChangesAsync();
        return product;
    }

    public async Task UpdateAsync(int id, UpdateProductRequest request)
    {
        var product = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Product {id} not found");

        if (request.Name != null) product.ProductName = request.Name;
        if (request.Description != null) product.Description = request.Description;
        if (request.Origin != null) product.Origin = request.Origin;
        if (request.Unit != null) product.Unit = request.Unit;
        if (request.BasePrice.HasValue) product.BasePrice = request.BasePrice.Value;
        if (request.IsOrganic.HasValue) product.IsOrganic = request.IsOrganic.Value;
        if (request.Status != null) product.Status = request.Status;
        if (request.CategoryId.HasValue) product.CategoryId = request.CategoryId.Value;
        if (request.ProviderId.HasValue) product.ProviderId = request.ProviderId.Value;
        product.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(product);
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
    }
}
