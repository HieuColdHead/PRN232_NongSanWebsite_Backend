using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProvidersController : BaseApiController
{
    private readonly IProviderService _service;

    public ProvidersController(IProviderService service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<Provider>>>> GetProviders()
    {
        var providers = await _service.GetAllAsync();
        return SuccessResponse(providers);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
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
    public async Task<ActionResult<ApiResponse<Provider>>> PostProvider(CreateProviderRequest request)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<Provider>("Forbidden", statusCode: 403);
        }

        var provider = await _service.CreateAsync(request);
        return SuccessResponse(provider, "Provider created successfully");
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> PutProvider(int id, UpdateProviderRequest request)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.UpdateAsync(id, request);
        return SuccessResponse("Provider updated successfully");
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteProvider(int id)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.DeleteAsync(id);
        return SuccessResponse("Provider deleted successfully");
    }
}
