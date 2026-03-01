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

    public async Task<CategoryDto?> GetByIdAsync(Guid id)
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
            CategoryId = Guid.NewGuid(),
            CategoryName = request.Name,
            Description = request.Description,
            ParentId = request.ParentId
        };

        await _repository.AddAsync(category);
        await _repository.SaveChangesAsync();

        // Reload with children
        return (await GetByIdAsync(category.CategoryId))!;
    }

    public async Task UpdateAsync(Guid id, UpdateCategoryRequest request)
    {
        var category = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Category {id} not found");

        if (request.Name != null) category.CategoryName = request.Name;
        if (request.Description != null) category.Description = request.Description;
        if (request.ParentId.HasValue) category.ParentId = request.ParentId.Value;

        await _repository.UpdateAsync(category);

        // If children list is provided, sync children
        if (request.Children != null)
        {
            var existingChildren = (await _repository.FindAsync(c => c.ParentId == id)).ToList();
            var existingChildIds = existingChildren.Select(c => c.CategoryId).ToHashSet();
            var requestedChildIds = request.Children.ToHashSet();

            // Add new children
            foreach (var childId in requestedChildIds.Where(childId => !existingChildIds.Contains(childId)))
            {
                var child = await _repository.GetByIdAsync(childId);
                if (child != null)
                {
                    child.ParentId = id;
                    await _repository.UpdateAsync(child);
                }
            }

            // Remove children that are no longer associated
            foreach (var child in existingChildren.Where(c => !requestedChildIds.Contains(c.CategoryId)))
            {
                child.ParentId = null; // Or handle as needed, e.g., soft delete
                await _repository.UpdateAsync(child);
            }
        }

        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
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

    private static CategoryDto MapToDto(Category category, ILookup<Guid?, Category>? childLookup = null)
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
