using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;

namespace BLL.Services;

public class CategoryService : ICategoryService
{
    private readonly IGenericRepository<Category> _repository;

    public CategoryService(IGenericRepository<Category> repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<Category>> GetAllAsync()
    {
        return _repository.GetAllAsync();
    }

    public Task<Category?> GetByIdAsync(int id)
    {
        return _repository.GetByIdAsync(id);
    }

    public async Task<Category> CreateAsync(CreateCategoryRequest request)
    {
        var category = new Category
        {
            CategoryName = request.Name,
            Description = request.Description,
            Children = request.Children
        };

        await _repository.AddAsync(category);
        await _repository.SaveChangesAsync();
        return category;
    }

    public async Task UpdateAsync(Category category)
    {
        await _repository.UpdateAsync(category);
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
    }
}
