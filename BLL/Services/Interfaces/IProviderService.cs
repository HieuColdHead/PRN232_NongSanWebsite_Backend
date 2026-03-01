using BLL.DTOs;
using DAL.Entity;

namespace BLL.Services.Interfaces;

public interface IProviderService
{
    Task<IEnumerable<Provider>> GetAllAsync();
    Task<Provider?> GetByIdAsync(Guid id);
    Task<Provider> CreateAsync(CreateProviderRequest request);
    Task UpdateAsync(Guid id, UpdateProviderRequest request);
    Task DeleteAsync(Guid id);
}
