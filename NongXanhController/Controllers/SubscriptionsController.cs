using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Authorize]
[Route("api/[controller]")]
public class SubscriptionsController : BaseApiController
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<SubscriptionDto>>>> GetMine()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _subscriptionService.GetUserSubscriptionsAsync(userId.Value);
        return SuccessResponse(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> Subscribe([FromBody] CreateSubscriptionRequest request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null) return Unauthorized();
        
        request.UserId = currentUserId.Value;

        try
        {
            var result = await _subscriptionService.SubscribeAsync(request);
            return SuccessResponse(result, "Subscription created successfully");
        }
        catch (Exception ex)
        {
            return ErrorResponse<SubscriptionDto>(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Cancel(Guid id)
    {
        var result = await _subscriptionService.CancelSubscriptionAsync(id);
        if (!result) return ErrorResponse<bool>("Failed to cancel subscription or not found", statusCode: 404);

        return SuccessResponse(true, "Subscription cancelled");
    }

    [HttpPatch("{id}/next-delivery")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateNextDelivery(Guid id, [FromQuery] DateTime date)
    {
        var result = await _subscriptionService.UpdateNextDeliveryDateAsync(id, date);
        if (!result) return ErrorResponse<bool>("Failed to update delivery date", statusCode: 404);

        return SuccessResponse(true, "Delivery date updated");
    }
}
