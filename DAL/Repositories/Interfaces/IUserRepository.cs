using DAL.Entity;

namespace DAL.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByFirebaseUidAsync(string firebaseUid);
    Task<bool> EmailExistsAsync(string email);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task SaveChangesAsync();
}
