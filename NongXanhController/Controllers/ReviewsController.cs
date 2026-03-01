using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
[Authorize]
public class ReviewsController : BaseApiController
{
    private readonly IReviewService _service;

    public ReviewsController(IReviewService service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<ReviewDto>>>> GetReviews([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        if (pageNumber < 1 || pageSize < 1)
        {
            return ErrorResponse<PagedResult<ReviewDto>>("Page number and page size must be greater than 0.");
        }

        var result = await _service.GetPagedAsync(pageNumber, pageSize);
        return SuccessResponse(result);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ReviewDto>>> GetReview(Guid id)
    {
        var review = await _service.GetByIdAsync(id);

        if (review == null)
        {
            return ErrorResponse<ReviewDto>("Review not found", statusCode: 404);
        }

        return SuccessResponse(review);
    }

    [HttpGet("product/{productId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<ReviewDto>>>> GetByProduct(Guid productId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        if (pageNumber < 1 || pageSize < 1)
        {
            return ErrorResponse<PagedResult<ReviewDto>>("Page number and page size must be greater than 0.");
        }

        var result = await _service.GetByProductIdAsync(productId, pageNumber, pageSize);
        return SuccessResponse(result);
    }

    /// <summary>
    /// UserId is automatically set from JWT token.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ReviewDto>>> PostReview(CreateReviewRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return ErrorResponse<ReviewDto>("Unauthorized", statusCode: 401);

        // Always assign UserId from JWT, ignore any value from request body
        request.UserId = userId.Value;

        var review = await _service.CreateAsync(request);
        return SuccessResponse(review, "Review created successfully");
    }

    /// <summary>
    /// Owner can update their own review. Admin can update any review (e.g. change status).
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> PutReview(Guid id, UpdateReviewRequest request)
    {
        var existing = await _service.GetByIdAsync(id);
        if (existing == null)
        {
            return ErrorResponse<object>("Review not found", statusCode: 404);
        }

        var userId = GetCurrentUserId();
        if (!IsAdmin())
        {
            if (existing.UserId != userId)
            {
                return ErrorResponse<object>("Forbidden", statusCode: 403);
            }

            // Non-admin cannot change review status
            request.Status = null;
        }

        await _service.UpdateAsync(id, request);
        return SuccessResponse("Review updated successfully");
    }

    /// <summary>
    /// Owner can delete their own review. Admin can delete any review.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteReview(Guid id)
    {
        var existing = await _service.GetByIdAsync(id);
        if (existing == null)
        {
            return ErrorResponse<object>("Review not found", statusCode: 404);
        }

        var userId = GetCurrentUserId();
        if (!IsAdmin() && existing.UserId != userId)
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.DeleteAsync(id);
        return SuccessResponse("Review deleted successfully");
    }
}
