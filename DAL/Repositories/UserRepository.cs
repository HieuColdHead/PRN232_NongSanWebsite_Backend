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

    public Task<User?> GetByFirebaseUidAsync(string firebaseUid)
    {
        return _dbContext.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
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
}
