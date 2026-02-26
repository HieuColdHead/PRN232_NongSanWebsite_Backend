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

    public async Task<IEnumerable<CategoryDto>> GetAllAsync()
    {
        var all = await _repository.GetAllAsync();
        var list = all.ToList();

        // Build tree: only return root categories (ParentId == null), with children nested
        var lookup = list.ToLookup(c => c.ParentId);
        var roots = list.Where(c => c.ParentId == null).ToList();

        return roots.Select(r => MapToDto(r, lookup));
    }

    public async Task<CategoryDto?> GetByIdAsync(int id)
    {
        var category = await _repository.GetByIdAsync(id);
        if (category == null) return null;

        // Load children
        var allChildren = await _repository.FindAsync(c => c.ParentId == id);
        var lookup = allChildren.ToLookup(c => c.ParentId);

        return MapToDto(category, lookup);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request)
    {
        var category = new Category
        {
            CategoryName = request.Name,
            Description = request.Description
        };

        await _repository.AddAsync(category);
        await _repository.SaveChangesAsync();

        // Create children under this parent
        if (request.Children.Count > 0)
        {
            foreach (var childName in request.Children)
            {
                var child = new Category
                {
                    CategoryName = childName,
                    ParentId = category.CategoryId
                };
                await _repository.AddAsync(child);
            }
            await _repository.SaveChangesAsync();
        }

        // Reload with children
        return (await GetByIdAsync(category.CategoryId))!;
    }

    public async Task UpdateAsync(int id, UpdateCategoryRequest request)
    {
        var category = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Category {id} not found");

        if (request.Name != null) category.CategoryName = request.Name;
        if (request.Description != null) category.Description = request.Description;

        await _repository.UpdateAsync(category);

        // If children list is provided, sync children
        if (request.Children != null)
        {
            var existingChildren = (await _repository.FindAsync(c => c.ParentId == id)).ToList();
            var existingNames = existingChildren.Select(c => c.CategoryName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var requestedNames = request.Children.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Add new children
            foreach (var name in request.Children.Where(n => !existingNames.Contains(n)))
            {
                var child = new Category
                {
                    CategoryName = name,
                    ParentId = id
                };
                await _repository.AddAsync(child);
            }

            // Soft-delete removed children
            foreach (var child in existingChildren.Where(c => !requestedNames.Contains(c.CategoryName)))
            {
                await _repository.DeleteAsync(child.CategoryId);
            }
        }

        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        // Also soft-delete all children
        var children = await _repository.FindAsync(c => c.ParentId == id);
        foreach (var child in children)
        {
            await _repository.DeleteAsync(child.CategoryId);
        }

        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
    }

    private static CategoryDto MapToDto(Category category, ILookup<int?, Category>? childLookup = null)
    {
        var dto = new CategoryDto
        {
            CategoryId = category.CategoryId,
            CategoryName = category.CategoryName,
            Description = category.Description,
            ParentId = category.ParentId
        };

        if (childLookup != null)
        {
            dto.Children = childLookup[category.CategoryId]
                .Select(c => MapToDto(c, childLookup))
                .ToList();
        }

        return dto;
    }
}
