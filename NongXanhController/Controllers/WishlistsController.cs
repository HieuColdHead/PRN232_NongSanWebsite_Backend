using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class WishlistsController : ControllerBase
{
    private readonly IWishlistService _wishlistService;

    public WishlistsController(IWishlistService wishlistService)
    {
        _wishlistService = wishlistService;
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetByUserId(Guid userId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _wishlistService.GetByUserIdAsync(userId, pageNumber, pageSize);
        return Ok(ApiResponse<PagedResult<WishlistDto>>.Ok(result, "Wishlist retrieved successfully."));
    }

    [HttpPost]
    public async Task<IActionResult> AddToWishlist([FromBody] CreateWishlistRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Invalid data."));

        try
        {
            var result = await _wishlistService.AddAsync(request);
            return Ok(ApiResponse<WishlistDto>.Ok(result, "Product added to wishlist successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}"));
        }
    }

    [HttpDelete("{userId:guid}/{productId:guid}")]
    public async Task<IActionResult> RemoveFromWishlist(Guid userId, Guid productId)
    {
        try
        {
            await _wishlistService.RemoveAsync(userId, productId);
            return Ok(ApiResponse<object>.Ok(null!, "Product removed from wishlist successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}"));
        }
    }
}
