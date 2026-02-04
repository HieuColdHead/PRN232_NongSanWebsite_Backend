using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
[Authorize]
public class ProductVariantsController : BaseApiController
{
    private readonly IProductVariantService _service;

    public ProductVariantsController(IProductVariantService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProductVariant>>>> GetProductVariants()
    {
        var variants = await _service.GetAllAsync();
        return SuccessResponse(variants);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ProductVariant>>> GetProductVariant(int id)
    {
        var productVariant = await _service.GetByIdAsync(id);

        if (productVariant == null)
        {
            return ErrorResponse<ProductVariant>("Product variant not found", statusCode: 404);
        }

        return SuccessResponse(productVariant);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductVariant>>> PostProductVariant(ProductVariant productVariant)
    {
        await _service.AddAsync(productVariant);
        return SuccessResponse(productVariant, "Product variant created successfully");
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> PutProductVariant(int id, ProductVariant productVariant)
    {
        if (id != productVariant.VariantId)
        {
            return ErrorResponse<object>("Product variant ID mismatch");
        }

        await _service.UpdateAsync(productVariant);
        return SuccessResponse("Product variant updated successfully");
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteProductVariant(int id)
    {
        await _service.DeleteAsync(id);
        return SuccessResponse("Product variant deleted successfully");
    }
}
