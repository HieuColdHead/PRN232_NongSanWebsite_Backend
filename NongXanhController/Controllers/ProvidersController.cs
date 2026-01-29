using BLL.Services.Interfaces;
using DAL.Entity;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProvidersController : ControllerBase
{
    private readonly IProviderService _service;

    public ProvidersController(IProviderService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Provider>>> GetProviders()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Provider>> GetProvider(int id)
    {
        var provider = await _service.GetByIdAsync(id);

        if (provider == null)
        {
            return NotFound();
        }

        return provider;
    }

    [HttpPost]
    public async Task<ActionResult<Provider>> PostProvider(Provider provider)
    {
        await _service.AddAsync(provider);

        return CreatedAtAction("GetProvider", new { id = provider.ProviderId }, provider);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutProvider(int id, Provider provider)
    {
        if (id != provider.ProviderId)
        {
            return BadRequest();
        }

        await _service.UpdateAsync(provider);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProvider(int id)
    {
        await _service.DeleteAsync(id);

        return NoContent();
    }
}
