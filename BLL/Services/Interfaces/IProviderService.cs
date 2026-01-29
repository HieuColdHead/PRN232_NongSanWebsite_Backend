using DAL.Entity;

namespace BLL.Services.Interfaces;

public interface IProviderService
{
    Task<IEnumerable<Provider>> GetAllAsync();
    Task<Provider?> GetByIdAsync(int id);
    Task AddAsync(Provider provider);
    Task UpdateAsync(Provider provider);
    Task DeleteAsync(int id);
}
