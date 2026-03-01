using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;
[ApiController]
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
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProductVariant>>>> GetProductVariants()
    {
        var variants = await _service.GetAllAsync();
        return SuccessResponse(variants);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ProductVariant>>> GetProductVariant(Guid id)
    {
        var productVariant = await _service.GetByIdAsync(id);

        if (productVariant == null)
        {
            return ErrorResponse<ProductVariant>("Product variant not found", statusCode: 404);
        }

        return SuccessResponse(productVariant);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductVariant>>> PostProductVariant(CreateProductVariantRequest request)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<ProductVariant>("Forbidden", statusCode: 403);
        }

        var variant = await _service.CreateAsync(request);
        return SuccessResponse(variant, "Product variant created successfully");
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> PutProductVariant(Guid id, UpdateProductVariantRequest request)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.UpdateAsync(id, request);
        return SuccessResponse("Product variant updated successfully");
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteProductVariant(Guid id)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.DeleteAsync(id);
        return SuccessResponse("Product variant deleted successfully");
    }
}
