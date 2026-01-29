using BLL.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[ApiController]
public class BaseApiController : ControllerBase
{
    protected ActionResult<ApiResponse<T>> SuccessResponse<T>(T data, string message = "Success")
    {
        return Ok(ApiResponse<T>.Ok(data, message));
    }

    protected ActionResult<ApiResponse<T>> ErrorResponse<T>(string message, List<string>? errors = null, int statusCode = 400)
    {
        var response = ApiResponse<T>.Fail(message, errors);
        return StatusCode(statusCode, response);
    }
    
    protected ActionResult<ApiResponse<object>> SuccessResponse(string message = "Success")
    {
        return Ok(ApiResponse<object>.Ok(null, message));
    }
}
