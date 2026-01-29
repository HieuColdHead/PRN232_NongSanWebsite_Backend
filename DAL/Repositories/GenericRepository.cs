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
        IQueryable<T> query = _dbSet;

        // Include ProductImages if T is Product
        if (typeof(T) == typeof(DAL.Entity.Product))
        {
            query = query.Include("ProductImages");
        }

        var all = await query.ToListAsync();
        
        // Filter out deleted items if the entity has an IsDeleted property
        if (typeof(T).GetProperty("IsDeleted") != null)
        {
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
        
        return all;
    }

    public async Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize)
    {
        IQueryable<T> query = _dbSet;

        // Include ProductImages if T is Product
        if (typeof(T) == typeof(DAL.Entity.Product))
        {
            query = query.Include("ProductImages");
        }

        // Filter out deleted items if the entity has an IsDeleted property
        // Note: For pagination, we should filter BEFORE paging.
        // Since we can't easily use reflection in IQueryable expression tree without building it dynamically,
        // we have a challenge.
        // However, we can try to build a dynamic expression or just fetch all and page in memory (bad for performance).
        // A better way is to assume the caller handles filtering or use a base class/interface constraint if possible.
        // But since we are stuck with reflection for now, let's try to build a lambda.
        
        if (typeof(T).GetProperty("IsDeleted") != null)
        {
            // x => x.IsDeleted == false
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, "IsDeleted");
            var constant = Expression.Constant(false);
            var equality = Expression.Equal(property, constant);
            var lambda = Expression.Lambda<Func<T, bool>>(equality, parameter);
            
            query = query.Where(lambda);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<T?> GetByIdAsync(object id)
    {
        T? entity;
        
        if (typeof(T) == typeof(DAL.Entity.Product))
        {
             var query = _dbSet.AsQueryable();
             query = query.Include("ProductImages");
             
             if (id is int productId)
             {
                 entity = await query.FirstOrDefaultAsync(e => EF.Property<int>(e, "ProductId") == productId);
             }
             else
             {
                 entity = await _dbSet.FindAsync(id);
             }
        }
        else
        {
            entity = await _dbSet.FindAsync(id);
        }

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
        IQueryable<T> query = _dbSet;
        
        if (typeof(T) == typeof(DAL.Entity.Product))
        {
            query = query.Include("ProductImages");
        }

        var results = await query.Where(predicate).ToListAsync();
        
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
