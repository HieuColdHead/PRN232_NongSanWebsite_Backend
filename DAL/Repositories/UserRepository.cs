using System.Linq;
using DAL.Data;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _dbContext;

    public UserRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> EmailExistsAsync(string email)
    {
        return _dbContext.Users.AnyAsync(u => u.Email == email);
    }

    public Task AddAsync(User user)
    {
        _dbContext.Users.Add(user);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user)
    {
        _dbContext.Users.Update(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }

    public async Task<(IEnumerable<User> Users, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize)
    {
        var query = _dbContext.Users.AsNoTracking().OrderBy(u => u.CreatedAt);
        var total = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    public Task<User?> GetByIdAsync(Guid id)
    {
        return _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User> CreateAsync(User user)
    {
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    public async Task<bool> UpdateAsync(Guid id, string? displayName, string? email, string? phoneNumber, bool? isActive)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            user.DisplayName = displayName;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            user.Email = email;
        }

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            user.PhoneNumber = phoneNumber;
        }

        if (isActive.HasValue)
        {
            user.IsActive = isActive.Value;
        }

        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return false;
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync();
        return true;
    }
}
