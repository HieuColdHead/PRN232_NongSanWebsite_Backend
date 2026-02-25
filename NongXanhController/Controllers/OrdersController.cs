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

    public OrdersController(IOrderService service)
    {
        _service = service;
    }

    /// <summary>
    /// Admin: get all orders (paged). Normal user: get only own orders.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<OrderDto>>>> GetOrders([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        if (pageNumber < 1 || pageSize < 1)
        {
            return ErrorResponse<PagedResult<OrderDto>>("Page number and page size must be greater than 0.");
        }

        if (IsAdmin())
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
    public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrder(int id)
    {
        var order = await _service.GetByIdAsync(id);

        if (order == null)
        {
            return ErrorResponse<OrderDto>("Order not found", statusCode: 404);
        }

        // Non-admin can only view their own orders
        var userId = GetCurrentUserId();
        if (!IsAdmin() && order.UserId != userId)
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
    /// Admin can update any order. Normal user can only update shipping address of their own pending orders.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> PutOrder(int id, UpdateOrderRequest request)
    {
        if (id != request.OrderId)
        {
            return ErrorResponse<object>("Order ID mismatch");
        }

        var existing = await _service.GetByIdAsync(id);
        if (existing == null)
        {
            return ErrorResponse<object>("Order not found", statusCode: 404);
        }

        var userId = GetCurrentUserId();
        if (!IsAdmin())
        {
            if (existing.UserId != userId)
            {
                return ErrorResponse<object>("Forbidden", statusCode: 403);
            }

            // Non-admin cannot change order status or VnPay status
            request.Status = null;
            request.VnPayStatus = null;
        }

        await _service.UpdateAsync(request);
        return SuccessResponse("Order updated successfully");
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteOrder(int id)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.DeleteAsync(id);
        return SuccessResponse("Order deleted successfully");
    }
}
