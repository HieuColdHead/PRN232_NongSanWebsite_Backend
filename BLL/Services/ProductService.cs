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

    public async Task UpdateAsync(Product product)
    {
        await _repository.UpdateAsync(product);
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
    }
}
