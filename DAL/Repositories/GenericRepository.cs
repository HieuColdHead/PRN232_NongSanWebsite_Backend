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

    public async Task<T?> GetByIdAsync(object id)
    {
        T? entity;
        
        if (typeof(T) == typeof(DAL.Entity.Product))
        {
             // For Product, we need to include images. FindAsync doesn't support Include directly easily without casting.
             // So we use FirstOrDefaultAsync.
             // Assuming the primary key is "ProductId" (int) for Product.
             // But GetByIdAsync takes object id.
             // We can try to build a query.
             
             // A generic way to find by key with Include is tricky.
             // Let's try to just use FindAsync first, and if it's a Product, we might need to load related data explicitly or use a different query.
             // However, FindAsync is efficient.
             
             // Better approach: Check if it is Product, then use query.
             var query = _dbSet.AsQueryable();
             query = query.Include("ProductImages");
             
             // We need to find by ID. Since we don't know the key name easily without metadata...
             // But we know for Product it is ProductId.
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
