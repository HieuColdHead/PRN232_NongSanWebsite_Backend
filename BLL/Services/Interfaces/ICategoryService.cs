using BLL.DTOs;
using DAL.Entity;

namespace BLL.Services.Interfaces;

public interface ICategoryService
{
    Task<IEnumerable<Category>> GetAllAsync();
    Task<Category?> GetByIdAsync(int id);
    Task<Category> CreateAsync(CreateCategoryRequest request);
    Task UpdateAsync(Category category);
    Task DeleteAsync(int id);
}
