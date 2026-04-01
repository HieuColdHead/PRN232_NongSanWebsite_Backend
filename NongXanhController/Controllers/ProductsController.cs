using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace NongXanhController.Controllers;
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : BaseApiController
{
    private readonly IProductService _service;
    private readonly ApplicationDbContext _context;

    public ProductsController(IProductService service, ApplicationDbContext context)
    {
        _service = service;
        _context = context;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductDto>>>> GetProducts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? categoryId = null)
    {
        if (pageNumber < 1 || pageSize < 1)
        {
            return ErrorResponse<PagedResult<ProductDto>>("Page number and page size must be greater than 0.");
        }

        if (categoryId.HasValue && categoryId.Value == Guid.Empty)
        {
            return ErrorResponse<PagedResult<ProductDto>>("categoryId is invalid.", statusCode: 400);
        }

        if (categoryId.HasValue)
        {
            try
            {
                var result = await _service.GetPagedByCategoryAsync(categoryId.Value, pageNumber, pageSize);
                return SuccessResponse(result);
            }
            catch (KeyNotFoundException ex)
            {
                return ErrorResponse<PagedResult<ProductDto>>(ex.Message, statusCode: 404);
            }
        }

        var allPaged = await _service.GetPagedAsync(pageNumber, pageSize);
        return SuccessResponse(allPaged);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetProduct(Guid id)
    {
        var product = await _service.GetByIdAsync(id);

        if (product == null)
        {
            return ErrorResponse<ProductDto>("Product not found", statusCode: 404);
        }

        return SuccessResponse(product);
    }

    [HttpGet("{id}/sold-quantity")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> GetSoldQuantity(Guid id)
    {
        var soldQuantity = await _service.GetSoldQuantityAsync(id);
        return SuccessResponse<object>(new { ProductId = id, SoldQuantity = soldQuantity });
    }

    [HttpGet("best-sellers")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProductBestSellerDto>>>> GetBestSellers(
        [FromQuery] int top = 10,
        [FromQuery] int? lastDays = null)
    {
        var result = await _service.GetBestSellersAsync(top, lastDays);
        return SuccessResponse(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductDto>>> PostProduct(CreateProductRequest request)
    {
        if (!IsAdminOrStaff())
        {
            return ErrorResponse<ProductDto>("Forbidden", statusCode: 403);
        }

        var product = await _service.CreateAsync(request);
        return SuccessResponse(product, "Product created successfully");
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> PutProduct(Guid id, UpdateProductRequest request)
    {
        if (!IsAdminOrStaff())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.UpdateAsync(id, request);

        return SuccessResponse("Product updated successfully");
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteProduct(Guid id)
    {
        if (!IsAdminOrStaff())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.DeleteAsync(id);

        return SuccessResponse("Product deleted successfully");
    }

    public sealed class ProductLookupItemDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
    }

    [HttpGet("lookup")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProductLookupItemDto>>>> LookupProducts(
        [FromQuery] string? query,
        [FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 100);
        var q = (query ?? string.Empty).Trim();

        var productsQuery = _context.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var qLower = q.ToLower();
            productsQuery = productsQuery.Where(p => p.ProductName.ToLower().Contains(qLower));
        }

        var items = await productsQuery
            .OrderBy(p => p.ProductName)
            .Take(limit)
            .Select(p => new ProductLookupItemDto
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName
            })
            .ToListAsync();

        return SuccessResponse<IEnumerable<ProductLookupItemDto>>(items);
    }

    public sealed class ProductVariantLookupDto
    {
        public Guid VariantId { get; set; }
        public string VariantName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? DiscountPrice { get; set; }
        public int StockQuantity { get; set; }
        public string? Sku { get; set; }
        public string? Status { get; set; }
        public Guid ProductId { get; set; }
    }

    [HttpGet("{productId}/variants")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProductVariantLookupDto>>>> GetProductVariantsByProduct(Guid productId)
    {
        if (productId == Guid.Empty)
        {
            return ErrorResponse<IEnumerable<ProductVariantLookupDto>>("Invalid productId.", statusCode: 400);
        }

        var productExists = await _context.Products
            .AsNoTracking()
            .AnyAsync(p => p.ProductId == productId && !p.IsDeleted);

        if (!productExists)
        {
            return ErrorResponse<IEnumerable<ProductVariantLookupDto>>("Product not found.", statusCode: 404);
        }

        var variants = await _context.ProductVariants
            .AsNoTracking()
            .Where(v => v.ProductId == productId && !v.IsDeleted)
            .OrderBy(v => v.VariantName)
            .Select(v => new ProductVariantLookupDto
            {
                VariantId = v.VariantId,
                VariantName = v.VariantName,
                Price = v.Price,
                DiscountPrice = v.DiscountPrice,
                StockQuantity = v.StockQuantity,
                Sku = v.Sku,
                Status = v.Status,
                ProductId = v.ProductId
            })
            .ToListAsync();

        return SuccessResponse<IEnumerable<ProductVariantLookupDto>>(variants);
    }
}
