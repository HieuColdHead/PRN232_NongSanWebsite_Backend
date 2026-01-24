using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DAL.Entity;

namespace DAL.Repositories.Interfaces;

public interface IUserRepository
{
    // Existing helpers
    Task<User?> GetByFirebaseUidAsync(string firebaseUid);
    Task<bool> EmailExistsAsync(string email);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task SaveChangesAsync();

    // CRUD + paging
    Task<(IEnumerable<User> Users, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize);
    Task<User?> GetByIdAsync(Guid id);
    Task<User> CreateAsync(User user);
    Task<bool> UpdateAsync(Guid id, string? displayName, string? email, string? phoneNumber, bool? isActive);
    Task<bool> DeleteAsync(Guid id);
}
