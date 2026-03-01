using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
[Authorize]
public class NotificationsController : BaseApiController
{
    private readonly INotificationService _service;

    public NotificationsController(INotificationService service)
    {
        _service = service;
    }

    /// <summary>
    /// Get notifications for the current user (from JWT). Admin can optionally query by userId.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationDto>>>> GetMyNotifications(
        [FromQuery] Guid? userId = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        if (pageNumber < 1 || pageSize < 1)
        {
            return ErrorResponse<PagedResult<NotificationDto>>("Page number and page size must be greater than 0.");
        }

        Guid targetUserId;

        if (userId.HasValue && IsAdmin())
        {
            // Admin can view any user's notifications
            targetUserId = userId.Value;
        }
        else
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId is null)
                return ErrorResponse<PagedResult<NotificationDto>>("Unauthorized", statusCode: 401);

            targetUserId = currentUserId.Value;
        }

        var result = await _service.GetByUserIdAsync(targetUserId, pageNumber, pageSize);
        return SuccessResponse(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<NotificationDto>>> GetNotification(Guid id)
    {
        var notification = await _service.GetByIdAsync(id);

        if (notification == null)
        {
            return ErrorResponse<NotificationDto>("Notification not found", statusCode: 404);
        }

        // Non-admin can only view their own notifications
        var currentUserId = GetCurrentUserId();
        if (!IsAdmin() && notification.UserId != currentUserId)
        {
            return ErrorResponse<NotificationDto>("Forbidden", statusCode: 403);
        }

        return SuccessResponse(notification);
    }

    /// <summary>
    /// Only Admin can create notifications for users.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<NotificationDto>>> PostNotification(CreateNotificationRequest request)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<NotificationDto>("Forbidden", statusCode: 403);
        }

        var notification = await _service.CreateAsync(request);
        return SuccessResponse(notification, "Notification created successfully");
    }

    [HttpPatch("{id}/read")]
    public async Task<ActionResult<ApiResponse<object>>> MarkAsRead(Guid id)
    {
        var notification = await _service.GetByIdAsync(id);
        if (notification == null)
        {
            return ErrorResponse<object>("Notification not found", statusCode: 404);
        }

        // Only owner can mark as read
        var currentUserId = GetCurrentUserId();
        if (notification.UserId != currentUserId)
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.MarkAsReadAsync(id);
        return SuccessResponse("Notification marked as read");
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteNotification(Guid id)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.DeleteAsync(id);
        return SuccessResponse("Notification deleted successfully");
    }
}
