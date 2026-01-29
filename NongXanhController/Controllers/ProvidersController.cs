using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
public class ProvidersController : BaseApiController
{
    private readonly IProviderService _service;

    public ProvidersController(IProviderService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<Provider>>>> GetProviders()
    {
        var providers = await _service.GetAllAsync();
        return SuccessResponse(providers);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<Provider>>> GetProvider(int id)
    {
        var provider = await _service.GetByIdAsync(id);

        if (provider == null)
        {
            return ErrorResponse<Provider>("Provider not found", statusCode: 404);
        }

        return SuccessResponse(provider);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Provider>>> PostProvider(Provider provider)
    {
        await _service.AddAsync(provider);
        return SuccessResponse(provider, "Provider created successfully");
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> PutProvider(int id, Provider provider)
    {
        if (id != provider.ProviderId)
        {
            return ErrorResponse<object>("Provider ID mismatch");
        }

        await _service.UpdateAsync(provider);
        return SuccessResponse("Provider updated successfully");
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteProvider(int id)
    {
        await _service.DeleteAsync(id);
        return SuccessResponse("Provider deleted successfully");
    }
}
