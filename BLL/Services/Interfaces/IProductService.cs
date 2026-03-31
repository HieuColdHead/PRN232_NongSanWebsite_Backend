using BLL.DTOs;
using DAL.Entity;

namespace BLL.Services.Interfaces;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetAllAsync();
    Task<PagedResult<ProductDto>> GetPagedAsync(int pageNumber, int pageSize);
    Task<ProductDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<ProductBestSellerDto>> GetBestSellersAsync(int top = 10, int? lastDays = null);
    Task<ProductDto> CreateAsync(CreateProductRequest request);
    Task UpdateAsync(Guid id, UpdateProductRequest request);
    Task DeleteAsync(Guid id);
}
