using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
[Authorize]
public class CartsController : BaseApiController
{
    private readonly ICartService _service;

    public CartsController(ICartService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<CartDto>>> GetCart()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return ErrorResponse<CartDto>("Unauthorized", statusCode: 401);

        var cart = await _service.GetByUserIdAsync(userId.Value);

        if (cart == null)
        {
            return ErrorResponse<CartDto>("Cart not found", statusCode: 404);
        }

        return SuccessResponse(cart);
    }

    [HttpPost("items")]
    public async Task<ActionResult<ApiResponse<CartDto>>> AddItem(AddCartItemRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return ErrorResponse<CartDto>("Unauthorized", statusCode: 401);

        var cart = await _service.AddItemAsync(userId.Value, request);
        return SuccessResponse(cart, "Item added to cart");
    }

    [HttpPut("items")]
    public async Task<ActionResult<ApiResponse<CartDto>>> UpdateItem(UpdateCartItemRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return ErrorResponse<CartDto>("Unauthorized", statusCode: 401);

        var cart = await _service.UpdateItemAsync(userId.Value, request);
        return SuccessResponse(cart, "Cart item updated");
    }

    [HttpDelete("items/{cartItemId}")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveItem(Guid cartItemId)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return ErrorResponse<object>("Unauthorized", statusCode: 401);

        await _service.RemoveItemAsync(userId.Value, cartItemId);
        return SuccessResponse("Item removed from cart");
    }

    [HttpDelete("clear")]
    public async Task<ActionResult<ApiResponse<object>>> ClearCart()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return ErrorResponse<object>("Unauthorized", statusCode: 401);

        await _service.ClearCartAsync(userId.Value);
        return SuccessResponse("Cart cleared");
    }
}
