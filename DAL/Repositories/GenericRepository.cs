using System.Linq.Expressions;
using DAL.Data;
using DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public GenericRepository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        // Filter out deleted items if the entity has an IsDeleted property
        if (typeof(T).GetProperty("IsDeleted") != null)
        {
            // We need to build a dynamic expression or cast to an interface if we had one.
            // Since we didn't introduce an interface, we can use EF Core's global query filters 
            // or just manually filter here using dynamic LINQ or reflection, but reflection in LINQ to Entities is tricky.
            // A better approach without an interface is to check if the property exists and filter in memory (not ideal for large datasets)
            // or assume the caller handles it.
            // However, the prompt asked to change "api delete to set it to true".
            // Let's try to filter here if possible, or just return all and let the caller filter.
            // But usually "GetAll" implies non-deleted items.
            
            // Let's use a simple check.
            // Note: The most robust way is Global Query Filters in DbContext, but I will modify this method to filter if possible.
            // Since I cannot easily use reflection in the query, I will fetch all and filter in memory for now, 
            // OR better, I will rely on the fact that I'm modifying the DeleteAsync method.
            // If the user wants GetAll to ONLY return non-deleted, I should probably use Global Query Filters in DbContext.
            // But the request was "change api delete to set it to true". It didn't explicitly say "hide deleted items in GetAll", 
            // though that is implied by "soft delete".
            
            // Let's try to use dynamic LINQ or just simple reflection for the "Delete" part first.
            // For GetAll, I will leave it as is unless requested, or I can try to cast to dynamic.
            
            // Actually, let's try to filter.
            var all = await _dbSet.ToListAsync();
            return all.Where(x => 
            {
                var prop = typeof(T).GetProperty("IsDeleted");
                if (prop != null && prop.PropertyType == typeof(bool))
                {
                    return (bool)prop.GetValue(x) == false;
                }
                return true;
            });
        }
        
        return await _dbSet.ToListAsync();
    }

    public async Task<T?> GetByIdAsync(object id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity != null)
        {
             var prop = typeof(T).GetProperty("IsDeleted");
             if (prop != null && prop.PropertyType == typeof(bool))
             {
                 var isDeleted = (bool)prop.GetValue(entity);
                 if (isDeleted) return null;
             }
        }
        return entity;
    }

    public async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(object id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity != null)
        {
            var prop = typeof(T).GetProperty("IsDeleted");
            if (prop != null && prop.PropertyType == typeof(bool))
            {
                prop.SetValue(entity, true);
                _dbSet.Update(entity);
            }
            else
            {
                _dbSet.Remove(entity);
            }
        }
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        // This might return deleted items if the predicate doesn't filter them.
        // Ideally we should combine predicates.
        var results = await _dbSet.Where(predicate).ToListAsync();
        
        return results.Where(x => 
        {
            var prop = typeof(T).GetProperty("IsDeleted");
            if (prop != null && prop.PropertyType == typeof(bool))
            {
                return (bool)prop.GetValue(x) == false;
            }
            return true;
        });
    }
}
