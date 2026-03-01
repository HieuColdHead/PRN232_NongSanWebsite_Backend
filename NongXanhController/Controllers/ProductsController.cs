using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : BaseApiController
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<Product>>>> GetProducts([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        if (pageNumber < 1 || pageSize < 1)
        {
            return ErrorResponse<PagedResult<Product>>("Page number and page size must be greater than 0.");
        }

        var result = await _service.GetPagedAsync(pageNumber, pageSize);
        return SuccessResponse(result);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<Product>>> GetProduct(Guid id)
    {
        var product = await _service.GetByIdAsync(id);

        if (product == null)
        {
            return ErrorResponse<Product>("Product not found", statusCode: 404);
        }

        return SuccessResponse(product);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Product>>> PostProduct(CreateProductRequest request)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<Product>("Forbidden", statusCode: 403);
        }

        var product = await _service.CreateAsync(request);
        return SuccessResponse(product, "Product created successfully");
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> PutProduct(Guid id, UpdateProductRequest request)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.UpdateAsync(id, request);

        return SuccessResponse("Product updated successfully");
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteProduct(Guid id)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.DeleteAsync(id);

        return SuccessResponse("Product deleted successfully");
    }
}
