using BLL.DTOs;
using DAL.Entity;

namespace BLL.Services.Interfaces;

public interface IProviderService
{
    Task<IEnumerable<Provider>> GetAllAsync();
    Task<Provider?> GetByIdAsync(int id);
    Task<Provider> CreateAsync(CreateProviderRequest request);
    Task UpdateAsync(int id, UpdateProviderRequest request);
    Task DeleteAsync(int id);
}
