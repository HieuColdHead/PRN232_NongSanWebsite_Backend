using System.Security.Claims;
using BLL.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[ApiController]
public class BaseApiController : ControllerBase
{
    private const string AdminRoleName = "Admin";

    protected Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    protected bool IsAdmin() => User.IsInRole(AdminRoleName);

    /// <summary>
    /// Returns the role of the current user from JWT claims, or null if not authenticated.
    /// </summary>
    protected string? GetCurrentUserRole()
    {
        return User.FindFirstValue(ClaimTypes.Role);
    }

    protected ActionResult<ApiResponse<T>> SuccessResponse<T>(T data, string message = "Success")
    {
        return Ok(ApiResponse<T>.Ok(data, message, role: GetCurrentUserRole()));
    }

    protected ActionResult<ApiResponse<T>> ErrorResponse<T>(string message, List<string>? errors = null, int statusCode = 400)
    {
        var response = ApiResponse<T>.Fail(message, errors);
        response.Role = GetCurrentUserRole();
        return StatusCode(statusCode, response);
    }
    
    protected ActionResult<ApiResponse<object>> SuccessResponse(string message = "Success")
    {
        return Ok(ApiResponse<object>.Ok(null, message, role: GetCurrentUserRole()));
    }
}
