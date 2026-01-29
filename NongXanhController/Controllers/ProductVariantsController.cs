using BLL.Services.Interfaces;
using DAL.Entity;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductVariantsController : ControllerBase
{
    private readonly IProductVariantService _service;

    public ProductVariantsController(IProductVariantService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductVariant>>> GetProductVariants()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductVariant>> GetProductVariant(int id)
    {
        var productVariant = await _service.GetByIdAsync(id);

        if (productVariant == null)
        {
            return NotFound();
        }

        return productVariant;
    }

    [HttpPost]
    public async Task<ActionResult<ProductVariant>> PostProductVariant(ProductVariant productVariant)
    {
        await _service.AddAsync(productVariant);

        return CreatedAtAction("GetProductVariant", new { id = productVariant.VariantId }, productVariant);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutProductVariant(int id, ProductVariant productVariant)
    {
        if (id != productVariant.VariantId)
        {
            return BadRequest();
        }

        await _service.UpdateAsync(productVariant);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProductVariant(int id)
    {
        await _service.DeleteAsync(id);

        return NoContent();
    }
}
