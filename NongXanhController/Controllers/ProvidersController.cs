using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProvidersController : ControllerBase
{
    private readonly IGenericRepository<Provider> _repository;

    public ProvidersController(IGenericRepository<Provider> repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Provider>>> GetProviders()
    {
        return Ok(await _repository.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Provider>> GetProvider(int id)
    {
        var provider = await _repository.GetByIdAsync(id);

        if (provider == null)
        {
            return NotFound();
        }

        return provider;
    }

    [HttpPost]
    public async Task<ActionResult<Provider>> PostProvider(Provider provider)
    {
        await _repository.AddAsync(provider);
        await _repository.SaveChangesAsync();

        return CreatedAtAction("GetProvider", new { id = provider.ProviderId }, provider);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutProvider(int id, Provider provider)
    {
        if (id != provider.ProviderId)
        {
            return BadRequest();
        }

        await _repository.UpdateAsync(provider);
        await _repository.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProvider(int id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();

        return NoContent();
    }
}
