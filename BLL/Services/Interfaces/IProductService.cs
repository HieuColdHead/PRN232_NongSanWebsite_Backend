using BLL.DTOs;
using DAL.Entity;

namespace BLL.Services.Interfaces;

public interface IProductService
{
    Task<IEnumerable<Product>> GetAllAsync();
    Task<PagedResult<Product>> GetPagedAsync(int pageNumber, int pageSize);
    Task<Product?> GetByIdAsync(int id);
    Task AddAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(int id);
}
