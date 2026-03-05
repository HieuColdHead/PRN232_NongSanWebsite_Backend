using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Data;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BLL.Services;

public class PaymentService : IPaymentService
{
    private readonly IGenericRepository<Payment> _repository;
    private readonly ApplicationDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IGenericRepository<Payment> repository,
        ApplicationDbContext context,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<PaymentService> logger)
    {
        _repository = repository;
        _context = context;
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PaymentDto?> GetByOrderIdAsync(Guid orderId)
    {
        var payments = await _repository.FindAsync(p => p.OrderId == orderId);
        var payment = payments.FirstOrDefault();
        if (payment == null) return null;
        return MapToDto(payment);
    }

    public async Task<PaymentDto> CreateAsync(CreatePaymentRequest request)
    {
        var payment = new Payment
        {
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = "Pending",
            OrderId = request.OrderId
        };

        await _repository.AddAsync(payment);
        await _repository.SaveChangesAsync();

        if (IsCodPaymentMethod(request.PaymentMethod))
        {
            try
            {
                await SendOrderConfirmationEmailAsync(request.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send COD order confirmation email for OrderId {OrderId}", request.OrderId);
            }
        }

        return MapToDto(payment);
    }

    public async Task<PaymentDto> UpdateStatusAsync(Guid paymentId, string status)
    {
        var payment = await _repository.GetByIdAsync(paymentId)
            ?? throw new KeyNotFoundException($"Payment {paymentId} not found");

        payment.PaymentStatus = status;
        if (status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            payment.PaidAt = DateTime.UtcNow;

        await _repository.UpdateAsync(payment);
        await _repository.SaveChangesAsync();

        return MapToDto(payment);
    }

    public async Task<VnPayCreateUrlResponse> CreateVnPayPaymentUrlAsync(CreateVnPayUrlRequest request)
    {
        if (request.OrderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId is required.", nameof(request.OrderId));
        }

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.OrderId == request.OrderId && !o.IsDeleted)
            ?? throw new KeyNotFoundException("Order not found.");

        if (order.FinalAmount <= 0)
        {
            throw new InvalidOperationException("Order amount must be greater than zero.");
        }

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.OrderId == order.OrderId && !p.IsDeleted);

        if (payment == null)
        {
            payment = new Payment
            {
                PaymentMethod = "VNPay",
                PaymentStatus = "Pending",
                OrderId = order.OrderId
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
        }
        else
        {
            if (!string.Equals(payment.PaymentMethod, "VNPay", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The order payment method is not VNPay.");
            }

            if (string.Equals(payment.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This order has already been paid.");
            }
        }

        var tmnCode = GetRequiredConfig("VnPay:TmnCode");
        var hashSecret = GetRequiredConfig("VnPay:HashSecret");
        var baseUrl = GetRequiredConfig("VnPay:BaseUrl");
        var returnUrl = GetRequiredConfig("VnPay:ReturnUrl");
        var txnRef = BuildTxnRef(order.OrderId);
        var amount = order.FinalAmount;

        var vnPayParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_Amount"] = Convert.ToInt64(amount * 100).ToString(),
            ["vnp_CreateDate"] = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = NormalizeIpAddress(request.ClientIp),
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = $"Thanh toan don hang {order.OrderNumber}",
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_TxnRef"] = txnRef
        };

        var signData = BuildQueryString(vnPayParams);
        var secureHash = ComputeHmacSha512(hashSecret, signData);
        LogVnPayRequestSignature(txnRef, signData, secureHash, returnUrl);
        var paymentUrl = $"{baseUrl}?{signData}&vnp_SecureHash={Uri.EscapeDataString(secureHash)}";

        _context.Transactions.Add(new Transaction
        {
            TransactionCode = txnRef,
            Amount = amount,
            TransactionDate = DateTime.UtcNow,
            Gateway = "VNPay",
            Status = "Pending",
            PaymentId = payment.PaymentId
        });

        await _context.SaveChangesAsync();

        return new VnPayCreateUrlResponse
        {
            OrderId = order.OrderId,
            PaymentId = payment.PaymentId,
            Amount = amount,
            TxnRef = txnRef,
            PaymentUrl = paymentUrl
        };
    }

    public async Task<VnPayReturnResult> ProcessVnPayReturnAsync(Dictionary<string, string> queryParams)
    {
        if (queryParams == null || queryParams.Count == 0)
        {
            throw new ArgumentException("VNPay callback query params are required.", nameof(queryParams));
        }

        var hashSecret = GetRequiredConfig("VnPay:HashSecret");
        var secureHash = queryParams.TryGetValue("vnp_SecureHash", out var h) ? h : null;
        var txnRef = queryParams.TryGetValue("vnp_TxnRef", out var t) ? t : null;
        var responseCode = queryParams.TryGetValue("vnp_ResponseCode", out var r) ? r : null;
        var transactionStatus = queryParams.TryGetValue("vnp_TransactionStatus", out var ts) ? ts : null;

        var signParams = queryParams
            .Where(kv => kv.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrWhiteSpace(kv.Value)
                         && !kv.Key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase)
                         && !kv.Key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var signData = BuildQueryString(signParams);
        var expectedHash = ComputeHmacSha512(hashSecret, signData);
        var signatureValid = !string.IsNullOrWhiteSpace(secureHash)
                             && secureHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
        LogVnPayResponseSignature(
            txnRef,
            signData,
            secureHash,
            expectedHash,
            signatureValid,
            responseCode,
            transactionStatus);

        var paymentSuccess = signatureValid
                             && responseCode == "00"
                             && transactionStatus == "00";

        var result = new VnPayReturnResult
        {
            SignatureValid = signatureValid,
            PaymentSuccess = paymentSuccess,
            ResponseCode = responseCode,
            TransactionStatus = transactionStatus,
            TxnRef = txnRef
        };

        if (string.IsNullOrWhiteSpace(txnRef))
        {
            result.Message = "Missing transaction reference.";
            return result;
        }

        var transaction = await _context.Transactions
            .Include(x => x.Payment)
            .FirstOrDefaultAsync(x => x.TransactionCode == txnRef && !x.IsDeleted);

        if (transaction == null)
        {
            result.Message = signatureValid
                ? "Transaction not found."
                : "Invalid signature and transaction not found.";
            return result;
        }

        if (string.Equals(transaction.Status, "Success", StringComparison.OrdinalIgnoreCase))
        {
            result.PaymentSuccess = true;
            result.Message = "Transaction already confirmed.";
            result.PaymentId = transaction.PaymentId;

            var existingOrderId = transaction.Payment?.OrderId;
            result.OrderId = existingOrderId;
            return result;
        }

        transaction.Status = paymentSuccess ? "Success" : "Failed";
        transaction.TransactionDate = DateTime.UtcNow;

        var payment = transaction.Payment
            ?? await _context.Payments.FirstOrDefaultAsync(p => p.PaymentId == transaction.PaymentId && !p.IsDeleted);

        if (payment != null)
        {
            payment.PaymentStatus = paymentSuccess ? "Paid" : "Failed";
            if (paymentSuccess)
            {
                payment.PaidAt ??= DateTime.UtcNow;
            }

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == payment.OrderId && !o.IsDeleted);
            if (order != null)
            {
                order.VnPayStatus = paymentSuccess ? "Paid" : "Failed";
                if (paymentSuccess && string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                {
                    order.Status = "Confirmed";
                }
            }

            result.PaymentId = payment.PaymentId;
            result.OrderId = payment.OrderId;
        }

        await _context.SaveChangesAsync();

        result.Message = paymentSuccess ? "VNPay payment confirmed." : "VNPay payment failed.";
        return result;
    }

    private static PaymentDto MapToDto(Payment payment)
    {
        return new PaymentDto
        {
            PaymentId = payment.PaymentId,
            PaymentMethod = payment.PaymentMethod,
            PaymentStatus = payment.PaymentStatus,
            PaidAt = payment.PaidAt,
            OrderId = payment.OrderId
        };
    }

    private static bool IsCodPaymentMethod(string? paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            return false;
        }

        var method = paymentMethod.Trim();
        return method.Equals("COD", StringComparison.OrdinalIgnoreCase)
            || method.Equals("CashOnDelivery", StringComparison.OrdinalIgnoreCase)
            || method.Equals("Cash On Delivery", StringComparison.OrdinalIgnoreCase);
    }

    private string GetRequiredConfig(string key)
    {
        var value = _configuration[key]?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing configuration value: {key}");
        }

        return value;
    }

    private static string NormalizeIpAddress(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return "127.0.0.1";
        }

        var trimmed = ip.Trim();
        if (trimmed == "::1")
        {
            return "127.0.0.1";
        }

        if (trimmed.Contains(':'))
        {
            return "127.0.0.1";
        }

        return trimmed;
    }

    private static string BuildTxnRef(Guid orderId)
    {
        return $"{DateTime.UtcNow:yyyyMMddHHmmss}{orderId.ToString("N")[..8]}";
    }

    private static string BuildQueryString(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        return string.Join("&", parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{WebUtility.UrlEncode(p.Key)}={WebUtility.UrlEncode(p.Value)}"));
    }

    private static string ComputeHmacSha512(string secret, string input)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var inputBytes = Encoding.UTF8.GetBytes(input);

        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private bool IsVnPaySignatureDebugEnabled()
    {
        return bool.TryParse(_configuration["VnPay:EnableSignatureDebug"], out var enabled) && enabled;
    }

    private void LogVnPayRequestSignature(string txnRef, string signData, string secureHash, string returnUrl)
    {
        if (!IsVnPaySignatureDebugEnabled())
        {
            return;
        }

        _logger.LogInformation(
            "VNPay request signature | TxnRef={TxnRef} | ReturnUrl={ReturnUrl} | SignData={SignData} | SecureHash={SecureHash}",
            txnRef,
            returnUrl,
            signData,
            secureHash);
    }

    private void LogVnPayResponseSignature(
        string? txnRef,
        string signData,
        string? providedHash,
        string expectedHash,
        bool signatureValid,
        string? responseCode,
        string? transactionStatus)
    {
        if (!IsVnPaySignatureDebugEnabled())
        {
            return;
        }

        _logger.LogInformation(
            "VNPay response signature | TxnRef={TxnRef} | SignatureValid={SignatureValid} | ResponseCode={ResponseCode} | TransactionStatus={TransactionStatus} | ProvidedHash={ProvidedHash} | ExpectedHash={ExpectedHash} | SignData={SignData}",
            txnRef,
            signatureValid,
            responseCode,
            transactionStatus,
            providedHash,
            expectedHash,
            signData);
    }

    private async Task SendOrderConfirmationEmailAsync(Guid orderId)
    {
        var order = await _context.Set<Order>()
            .AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.OrderDetails)
                .ThenInclude(d => d.ProductVariant!)
                    .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        var email = order?.User?.Email;
        if (order == null || string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var subject = $"[NongXanh] Xác nhận đơn hàng COD #{order.OrderNumber}";
        var body = BuildOrderConfirmationBody(order);
        await _emailSender.SendAsync(email, subject, body);
    }

    private static string BuildOrderConfirmationBody(Order order)
    {
        var culture = CultureInfo.GetCultureInfo("vi-VN");
        static string FormatVnd(decimal amount, CultureInfo c) => $"{amount.ToString("N0", c)} VND";

        var sb = new StringBuilder();
        sb.AppendLine("Cảm ơn bạn đã đặt hàng COD tại NongXanh!");
        sb.AppendLine();
        sb.AppendLine($"Mã đơn hàng: {order.OrderNumber}");
        sb.AppendLine($"Ngày đặt: {order.OrderDate:dd/MM/yyyy HH:mm}");
        sb.AppendLine($"Địa chỉ nhận hàng: {order.ShippingAddress ?? "(Không có)"}");
        sb.AppendLine();
        sb.AppendLine("Chi tiết đơn hàng:");

        foreach (var detail in order.OrderDetails)
        {
            var variantName = detail.ProductVariant?.VariantName ?? "Sản phẩm";
            var productName = detail.ProductVariant?.Product?.ProductName;
            var displayName = string.IsNullOrWhiteSpace(productName)
                ? variantName
                : $"{productName} - {variantName}";

            sb.AppendLine($"- {displayName} x{detail.Quantity}: {FormatVnd(detail.SubTotal, culture)}");
        }

        sb.AppendLine();
        sb.AppendLine($"Tạm tính: {FormatVnd(order.TotalAmount, culture)}");
        sb.AppendLine($"Phí vận chuyển: {FormatVnd(order.ShippingFee, culture)}");
        sb.AppendLine($"Giảm giá: {FormatVnd(order.DiscountAmount, culture)}");
        sb.AppendLine($"Tổng thanh toán (COD): {FormatVnd(order.FinalAmount, culture)}");
        sb.AppendLine();
        sb.AppendLine("Đơn hàng sẽ được xử lý và giao đến bạn sớm nhất có thể.");
        sb.AppendLine("Trân trọng,");
        sb.AppendLine("Đội ngũ NongXanh");

        return sb.ToString();
    }
}
