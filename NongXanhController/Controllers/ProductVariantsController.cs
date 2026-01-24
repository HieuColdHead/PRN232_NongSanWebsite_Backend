using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductVariantsController : ControllerBase
{
    private readonly IGenericRepository<ProductVariant> _repository;

    public ProductVariantsController(IGenericRepository<ProductVariant> repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductVariant>>> GetProductVariants()
    {
        return Ok(await _repository.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductVariant>> GetProductVariant(int id)
    {
        var productVariant = await _repository.GetByIdAsync(id);

        if (productVariant == null)
        {
            return NotFound();
        }

        return productVariant;
    }

    [HttpPost]
    public async Task<ActionResult<ProductVariant>> PostProductVariant(ProductVariant productVariant)
    {
        await _repository.AddAsync(productVariant);
        await _repository.SaveChangesAsync();

        return CreatedAtAction("GetProductVariant", new { id = productVariant.VariantId }, productVariant);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutProductVariant(int id, ProductVariant productVariant)
    {
        if (id != productVariant.VariantId)
        {
            return BadRequest();
        }

        await _repository.UpdateAsync(productVariant);
        await _repository.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProductVariant(int id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();

        return NoContent();
    }
}
