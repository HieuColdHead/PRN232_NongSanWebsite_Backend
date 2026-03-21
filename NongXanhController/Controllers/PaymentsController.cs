using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
[Authorize]
public class PaymentsController : BaseApiController
{
    private readonly IPaymentService _service;
    private readonly IOrderService _orderService;
    private readonly IConfiguration _configuration;

    public PaymentsController(IPaymentService service, IOrderService orderService, IConfiguration configuration)
    {
        _service = service;
        _orderService = orderService;
        _configuration = configuration;
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
        var frontendReturnUrl = _configuration["VnPay:FrontendReturnUrl"]?.Trim();

        try
        {
            var result = await _service.ProcessVnPayReturnAsync(query);
            if (!string.IsNullOrWhiteSpace(frontendReturnUrl))
            {
                return Redirect(BuildFrontendReturnUrl(frontendReturnUrl, query, result));
            }

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
            if (!string.IsNullOrWhiteSpace(frontendReturnUrl))
            {
                return Redirect(BuildFrontendReturnUrl(frontendReturnUrl, query, new VnPayReturnResult
                {
                    SignatureValid = false,
                    PaymentSuccess = false,
                    Message = ex.Message
                }));
            }

            return ErrorResponse<VnPayReturnResult>(ex.Message, statusCode: 400);
        }
        catch (InvalidOperationException ex)
        {
            if (!string.IsNullOrWhiteSpace(frontendReturnUrl))
            {
                return Redirect(BuildFrontendReturnUrl(frontendReturnUrl, query, new VnPayReturnResult
                {
                    SignatureValid = false,
                    PaymentSuccess = false,
                    Message = ex.Message
                }));
            }

            return ErrorResponse<VnPayReturnResult>(ex.Message, statusCode: 400);
        }
    }

    [AllowAnonymous]
    [HttpPost("vnpay/confirm")]
    public async Task<ActionResult<ApiResponse<VnPayReturnResult>>> VnPayConfirmFromClient([FromBody] Dictionary<string, string> query)
    {
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

            return SuccessResponse(result, "VNPay payment confirmed successfully");
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

    private static string BuildFrontendReturnUrl(string frontendReturnUrl, Dictionary<string, string> originalQuery, VnPayReturnResult result)
    {
        var queryBuilder = new QueryBuilder();

        foreach (var kv in originalQuery)
        {
            if (!string.IsNullOrWhiteSpace(kv.Key) && kv.Value != null)
            {
                queryBuilder.Add(kv.Key, kv.Value);
            }
        }

        queryBuilder.Add("nx_signatureValid", result.SignatureValid ? "1" : "0");
        queryBuilder.Add("nx_paymentSuccess", result.PaymentSuccess ? "1" : "0");

        if (!string.IsNullOrWhiteSpace(result.ResponseCode))
        {
            queryBuilder.Add("nx_responseCode", result.ResponseCode);
        }

        if (!string.IsNullOrWhiteSpace(result.TransactionStatus))
        {
            queryBuilder.Add("nx_transactionStatus", result.TransactionStatus);
        }

        if (!string.IsNullOrWhiteSpace(result.TxnRef))
        {
            queryBuilder.Add("nx_txnRef", result.TxnRef);
        }

        if (result.OrderId.HasValue)
        {
            queryBuilder.Add("nx_orderId", result.OrderId.Value.ToString());
        }

        if (result.PaymentId.HasValue)
        {
            queryBuilder.Add("nx_paymentId", result.PaymentId.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            queryBuilder.Add("nx_message", result.Message);
        }

        return $"{frontendReturnUrl}{queryBuilder.ToQueryString()}";
    }
}
