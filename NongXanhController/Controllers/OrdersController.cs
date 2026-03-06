using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
[Authorize]
public class OrdersController : BaseApiController
{
    private readonly IOrderService _service;
    private readonly IPaymentService _paymentService;

    public OrdersController(IOrderService service, IPaymentService paymentService)
    {
        _service = service;
        _paymentService = paymentService;
    }

    /// <summary>
    /// Admin or Staff: get all orders (paged). Normal user: get only own orders.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<OrderDto>>>> GetOrders([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        if (pageNumber < 1 || pageSize < 1)
        {
            return ErrorResponse<PagedResult<OrderDto>>("Page number and page size must be greater than 0.");
        }

        if (IsAdminOrStaff())
        {
            var result = await _service.GetPagedAsync(pageNumber, pageSize);
            return SuccessResponse(result);
        }

        var userId = GetCurrentUserId();
        if (userId is null) return ErrorResponse<PagedResult<OrderDto>>("Unauthorized", statusCode: 401);

        var userOrders = await _service.GetByUserIdAsync(userId.Value, pageNumber, pageSize);
        return SuccessResponse(userOrders);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrder(Guid id)
    {
        var order = await _service.GetByIdAsync(id);

        if (order == null)
        {
            return ErrorResponse<OrderDto>("Order not found", statusCode: 404);
        }

        // Non-admin/non-staff can only view their own orders
        var userId = GetCurrentUserId();
        if (!IsAdminOrStaff() && order.UserId != userId)
        {
            return ErrorResponse<OrderDto>("Forbidden", statusCode: 403);
        }

        return SuccessResponse(order);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<OrderDto>>> PostOrder(CreateOrderRequest request)
    {
        // Always assign UserId from JWT token, ignore any value from request body
        var userId = GetCurrentUserId();
        if (userId is null) return ErrorResponse<OrderDto>("Unauthorized", statusCode: 401);

        request.UserId = userId.Value;

        var order = await _service.CreateAsync(request);
        return SuccessResponse(order, "Order created successfully");
    }

    /// <summary>
    /// Preview checkout bill from selected cart items + optional voucher.
    /// </summary>
    [HttpPost("checkout/preview")]
    public async Task<ActionResult<ApiResponse<CheckoutPreviewDto>>> PreviewCheckout(CheckoutPreviewRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return ErrorResponse<CheckoutPreviewDto>("Unauthorized", statusCode: 401);

        try
        {
            var preview = await _service.PreviewCheckoutAsync(userId.Value, request);
            return SuccessResponse(preview);
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorResponse<CheckoutPreviewDto>(ex.Message, statusCode: 404);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<CheckoutPreviewDto>(ex.Message, statusCode: 400);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse<CheckoutPreviewDto>(ex.Message, statusCode: 400);
        }
    }

    /// <summary>
    /// Create order from selected cart items, apply voucher, and create payment.
    /// If payment method is VNPay, returns payment URL for redirect.
    /// </summary>
    [HttpPost("checkout")]
    public async Task<ActionResult<ApiResponse<CheckoutOrderResultDto>>> Checkout(CheckoutOrderRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return ErrorResponse<CheckoutOrderResultDto>("Unauthorized", statusCode: 401);

        try
        {
            var result = await _service.CheckoutFromCartAsync(userId.Value, request);

            if (string.Equals(request.PaymentMethod, "VNPay", StringComparison.OrdinalIgnoreCase)
                || string.Equals(request.PaymentMethod, "VNPAY", StringComparison.OrdinalIgnoreCase))
            {
                var vnPayResult = await _paymentService.CreateVnPayPaymentUrlAsync(new CreateVnPayUrlRequest
                {
                    OrderId = result.Order.OrderId,
                    ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString()
                });

                result.PaymentUrl = vnPayResult.PaymentUrl;
                result.VnPayTransactionRef = vnPayResult.TxnRef;
            }

            return SuccessResponse(result, "Checkout completed successfully");
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorResponse<CheckoutOrderResultDto>(ex.Message, statusCode: 404);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<CheckoutOrderResultDto>(ex.Message, statusCode: 400);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse<CheckoutOrderResultDto>(ex.Message, statusCode: 400);
        }
    }

    /// <summary>
    /// Admin or Staff can update any order. Normal user can only update shipping address of their own pending orders.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> PutOrder(Guid id, UpdateOrderRequest request)
    {
        var existing = await _service.GetByIdAsync(id);
        if (existing == null)
        {
            return ErrorResponse<object>("Order not found", statusCode: 404);
        }

        var userId = GetCurrentUserId();
        if (!IsAdminOrStaff())
        {
            if (existing.UserId != userId)
            {
                return ErrorResponse<object>("Forbidden", statusCode: 403);
            }

            // Non-admin/non-staff cannot change order status or VnPay status
            request.Status = null;
            request.VnPayStatus = null;
        }

        await _service.UpdateAsync(id, request);
        return SuccessResponse("Order updated successfully");
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteOrder(Guid id)
    {
        if (!IsAdminOrStaff())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.DeleteAsync(id);
        return SuccessResponse("Order deleted successfully");
    }
}
