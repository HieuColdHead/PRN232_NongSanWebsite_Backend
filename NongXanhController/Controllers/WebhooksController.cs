using System.Linq;
using BLL.DTOs;
using BLL.DTOs.Ghn;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController : BaseApiController
{
    private readonly IShipmentService _shipmentService;

    public WebhooksController(IShipmentService shipmentService)
    {
        _shipmentService = shipmentService;
    }

    [AllowAnonymous]
    [HttpPost("ghn")]
    public async Task<ActionResult<ApiResponse<object>>> GhnWebhook([FromBody] GhnWebhookRequest request)
    {
        var tokenHeader = Request.Headers["Token"].FirstOrDefault()
            ?? Request.Headers["X-GHN-Token"].FirstOrDefault();

        try
        {
            await _shipmentService.ProcessGhnWebhookAsync(request, tokenHeader);
            return SuccessResponse("GHN webhook processed successfully");
        }
        catch (UnauthorizedAccessException ex)
        {
            return ErrorResponse<object>(ex.Message, statusCode: 401);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<object>(ex.Message, statusCode: 400);
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorResponse<object>(ex.Message, statusCode: 404);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse<object>(ex.Message, statusCode: 400);
        }
    }
}
