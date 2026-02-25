using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<ActionResult<ApiResponse<PaymentDto>>> GetByOrder(int orderId)
    {
        // Verify the user owns this order (or is admin)
        var order = await _orderService.GetByIdAsync(orderId);
        if (order == null)
        {
            return ErrorResponse<PaymentDto>("Order not found", statusCode: 404);
        }

        var userId = GetCurrentUserId();
        if (!IsAdmin() && order.UserId != userId)
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
        // Verify the user owns the order being paid for (or is admin)
        var order = await _orderService.GetByIdAsync(request.OrderId);
        if (order == null)
        {
            return ErrorResponse<PaymentDto>("Order not found", statusCode: 404);
        }

        var userId = GetCurrentUserId();
        if (!IsAdmin() && order.UserId != userId)
        {
            return ErrorResponse<PaymentDto>("Forbidden", statusCode: 403);
        }

        var payment = await _service.CreateAsync(request);
        return SuccessResponse(payment, "Payment created successfully");
    }

    /// <summary>
    /// Admin only: update payment status.
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> UpdateStatus(int id, [FromBody] string status)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<PaymentDto>("Forbidden", statusCode: 403);
        }

        var payment = await _service.UpdateStatusAsync(id, status);
        return SuccessResponse(payment, "Payment status updated");
    }
}
