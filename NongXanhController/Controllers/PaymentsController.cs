using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
[Authorize]
public class PaymentsController : BaseApiController
{
    private readonly IPaymentService _service;
    private readonly IOrderService _orderService;

    public PaymentsController(IPaymentService service, IOrderService orderService)
    {
        _service = service;
        _orderService = orderService;
    }

    [HttpGet("order/{orderId}")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> GetByOrder(Guid orderId)
    {
        // Verify the user owns this order (or is admin/staff)
        var order = await _orderService.GetByIdAsync(orderId);
        if (order == null)
        {
            return ErrorResponse<PaymentDto>("Order not found", statusCode: 404);
        }

        var userId = GetCurrentUserId();
        if (!IsAdminOrStaff() && order.UserId != userId)
        {
            return ErrorResponse<PaymentDto>("Forbidden", statusCode: 403);
        }

        var payment = await _service.GetByOrderIdAsync(orderId);

        if (payment == null)
        {
            return ErrorResponse<PaymentDto>("Payment not found", statusCode: 404);
        }

        return SuccessResponse(payment);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> PostPayment(CreatePaymentRequest request)
    {
        // Verify the user owns the order being paid for (or is admin/staff)
        var order = await _orderService.GetByIdAsync(request.OrderId);
        if (order == null)
        {
            return ErrorResponse<PaymentDto>("Order not found", statusCode: 404);
        }

        var userId = GetCurrentUserId();
        if (!IsAdminOrStaff() && order.UserId != userId)
        {
            return ErrorResponse<PaymentDto>("Forbidden", statusCode: 403);
        }

        var payment = await _service.CreateAsync(request);
        return SuccessResponse(payment, "Payment created successfully");
    }

    [HttpPost("vnpay/create-url")]
    public async Task<ActionResult<ApiResponse<VnPayCreateUrlResponse>>> CreateVnPayUrl(CreateVnPayUrlRequest request)
    {
        var order = await _orderService.GetByIdAsync(request.OrderId);
        if (order == null)
        {
            return ErrorResponse<VnPayCreateUrlResponse>("Order not found", statusCode: 404);
        }

        var userId = GetCurrentUserId();
        if (!IsAdminOrStaff() && order.UserId != userId)
        {
            return ErrorResponse<VnPayCreateUrlResponse>("Forbidden", statusCode: 403);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.ClientIp))
            {
                request.ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            }

            var result = await _service.CreateVnPayPaymentUrlAsync(request);
            return SuccessResponse(result, "VNPay payment URL created");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorResponse<VnPayCreateUrlResponse>(ex.Message, statusCode: 404);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<VnPayCreateUrlResponse>(ex.Message, statusCode: 400);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse<VnPayCreateUrlResponse>(ex.Message, statusCode: 400);
        }
    }

    [AllowAnonymous]
    [HttpGet("vnpay-return")]
    public async Task<ActionResult<ApiResponse<VnPayReturnResult>>> VnPayReturn()
    {
        var query = Request.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

        try
        {
            var result = await _service.ProcessVnPayReturnAsync(query);
            if (!result.SignatureValid)
            {
                return ErrorResponse<VnPayReturnResult>(result.Message ?? "Invalid VNPay signature.", statusCode: 400);
            }

            if (!result.PaymentSuccess)
            {
                return ErrorResponse<VnPayReturnResult>(result.Message ?? "VNPay payment failed.", statusCode: 400);
            }

            return SuccessResponse(result, "VNPay payment callback processed successfully");
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<VnPayReturnResult>(ex.Message, statusCode: 400);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse<VnPayReturnResult>(ex.Message, statusCode: 400);
        }
    }

    /// <summary>
    /// Admin or Staff: update payment status.
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> UpdateStatus(Guid id, [FromBody] string status)
    {
        if (!IsAdminOrStaff())
        {
            return ErrorResponse<PaymentDto>("Forbidden", statusCode: 403);
        }

        var payment = await _service.UpdateStatusAsync(id, status);
        return SuccessResponse(payment, "Payment status updated");
    }
}
