using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;

namespace BLL.Services;

public class ProviderService : IProviderService
{
    private readonly IGenericRepository<Provider> _repository;

    public ProviderService(IGenericRepository<Provider> repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<Provider>> GetAllAsync()
    {
        return _repository.GetAllAsync();
    }

    public Task<Provider?> GetByIdAsync(int id)
    {
        return _repository.GetByIdAsync(id);
    }

    public async Task AddAsync(Provider provider)
    {
        await _repository.AddAsync(provider);
        await _repository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Provider provider)
    {
        await _repository.UpdateAsync(provider);
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
    }
}
