using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class WishlistsController : BaseApiController
{
    private readonly IWishlistService _wishlistService;

    public WishlistsController(IWishlistService wishlistService)
    {
        _wishlistService = wishlistService;
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<ApiResponse<PagedResult<WishlistDto>>>> GetByUserId(Guid userId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _wishlistService.GetByUserIdAsync(userId, pageNumber, pageSize);
        return SuccessResponse(result, "Wishlist retrieved successfully.");
    }

    [HttpGet("my")]
    public async Task<ActionResult<ApiResponse<PagedResult<WishlistDto>>>> GetMyWishlist([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(ApiResponse<object>.Fail("User not found"));

        var result = await _wishlistService.GetByUserIdAsync(userId.Value, pageNumber, pageSize);
        return SuccessResponse(result, "Wishlist retrieved successfully.");
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WishlistDto>>> AddToWishlist([FromBody] CreateWishlistRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(ApiResponse<object>.Fail("User not found"));

        if (!ModelState.IsValid)
            return ErrorResponse<WishlistDto>("Invalid data.");

        try
        {
            var result = await _wishlistService.AddAsync(userId.Value, request);
            return SuccessResponse(result, "Product added to wishlist successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorResponse<WishlistDto>(ex.Message, statusCode: 404);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse<WishlistDto>(ex.Message);
        }
        catch (Exception ex)
        {
            return ErrorResponse<WishlistDto>($"Internal server error: {ex.Message}", statusCode: 500);
        }
    }

    [HttpDelete("{productId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveFromWishlist(Guid productId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(ApiResponse<object>.Fail("User not found"));

        try
        {
            await _wishlistService.RemoveAsync(userId.Value, productId);
            return SuccessResponse("Product removed from wishlist successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorResponse<object>(ex.Message, statusCode: 404);
        }
        catch (Exception ex)
        {
            return ErrorResponse<object>($"Internal server error: {ex.Message}", statusCode: 500);
        }
    }
}
