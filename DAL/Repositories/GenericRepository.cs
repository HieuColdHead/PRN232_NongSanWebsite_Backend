using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DAL.Data;
using DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories
{
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

            if (typeof(T) == typeof(DAL.Entity.Product))
            {
                query = query.Include("ProductImages");
            }

            var all = await query.ToListAsync();
            
            if (typeof(T).GetProperty("IsDeleted") != null)
            {
                return all.Where(x => 
                {
                    var prop = typeof(T).GetProperty("IsDeleted");
                    return prop != null && prop.PropertyType == typeof(bool) && (bool)prop.GetValue(x) == false;
                });
            }
            
            return all;
        }

        public async Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize)
        {
            IQueryable<T> query = _dbSet;

            if (typeof(T) == typeof(DAL.Entity.Product))
            {
                query = query.Include("ProductImages");
            }

            if (typeof(T).GetProperty("IsDeleted") != null)
            {
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
            
            var query = _dbSet.AsQueryable();

            if (typeof(T) == typeof(DAL.Entity.Product))
            {
                 query = query.Include("ProductImages");
            }
             
            if (id is Guid guidId)
            {
                // This is a more robust way to query by primary key for any entity
                var parameter = Expression.Parameter(typeof(T), "e");
                var property = Expression.Property(parameter, GetPrimaryKeyPropertyName());
                var constant = Expression.Constant(guidId);
                var equality = Expression.Equal(property, constant);
                var lambda = Expression.Lambda<Func<T, bool>>(equality, parameter);
                entity = await query.FirstOrDefaultAsync(lambda);
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

        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        public Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(object id)
        {
            var entity = await GetByIdAsync(id); // Use GetByIdAsync to respect IsDeleted flag
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
                return prop == null || prop.PropertyType != typeof(bool) || (bool)prop.GetValue(x) == false;
            });
        }

        public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            IQueryable<T> query = _dbSet;

            if (typeof(T).GetProperty("IsDeleted") != null)
            {
                var parameter = Expression.Parameter(typeof(T), "x");
                var property = Expression.Property(parameter, "IsDeleted");
                var constant = Expression.Constant(false);
                var equality = Expression.Equal(property, constant);
                var lambda = Expression.Lambda<Func<T, bool>>(equality, parameter);
                
                query = query.Where(lambda);
            }
            
            return await query.FirstOrDefaultAsync(predicate);
        }

        private string GetPrimaryKeyPropertyName()
        {
            var key = _context.Model.FindEntityType(typeof(T))?.FindPrimaryKey()?.Properties.FirstOrDefault();
            return key?.Name ?? "Id";
        }
    }
}
