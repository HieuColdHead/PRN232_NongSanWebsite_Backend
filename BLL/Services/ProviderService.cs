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

    public Task<Provider?> GetByIdAsync(Guid id)
    {
        return _repository.GetByIdAsync(id);
    }

    public async Task<Provider> CreateAsync(CreateProviderRequest request)
    {
        var provider = new Provider
        {
            ProviderId = Guid.NewGuid(),
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

    public async Task UpdateAsync(Guid id, UpdateProviderRequest request)
    {
        var provider = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Provider {id} not found");

        if (request.Name != null) provider.ProviderName = request.Name;
        if (request.Description != null) provider.Description = request.Description;
        if (request.Address != null) provider.Address = request.Address;
        if (request.Phone != null) provider.Phone = request.Phone;
        if (request.Email != null) provider.Email = request.Email;
        if (request.RatingAverage.HasValue) provider.RatingAverage = request.RatingAverage;
        if (request.Status != null) provider.Status = request.Status;

        await _repository.UpdateAsync(provider);
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
    }
}
