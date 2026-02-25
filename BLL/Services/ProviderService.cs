using BLL.DTOs;
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

    public async Task<Provider> CreateAsync(CreateProviderRequest request)
    {
        var provider = new Provider
        {
            ProviderName = request.Name,
            Description = request.Description,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            RatingAverage = request.RatingAverage,
            Status = request.Status
        };

        await _repository.AddAsync(provider);
        await _repository.SaveChangesAsync();
        return provider;
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
