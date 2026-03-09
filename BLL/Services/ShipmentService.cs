using System.Globalization;
using BLL.DTOs;
using BLL.DTOs.Ghn;
using BLL.Services.Interfaces;
using DAL.Data;
using DAL.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BLL.Services;

public class ShipmentService : IShipmentService
{
    private readonly ApplicationDbContext _context;
    private readonly IGhnService _ghnService;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ShipmentService> _logger;

    public ShipmentService(
        ApplicationDbContext context,
        IGhnService ghnService,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<ShipmentService> logger)
    {
        _context = context;
        _ghnService = ghnService;
        _notificationService = notificationService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ShipmentDto?> GetByOrderIdAsync(Guid orderId)
    {
        var shipment = await _context.Shipments
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrderId == orderId && !s.IsDeleted);

        return shipment == null ? null : MapToDto(shipment);
    }

    public async Task<ShipmentDto?> CreateShipmentForOrderAsync(Guid orderId, string triggerSource, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.OrderDetails)
                .ThenInclude(d => d.ProductVariant!)
                    .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(o => o.OrderId == orderId && !o.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Order {orderId} not found.");

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.OrderId == orderId && !p.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Payment for order {orderId} not found.");

        if (IsVnPay(payment.PaymentMethod) && !string.Equals(payment.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var shipment = await _context.Shipments
            .FirstOrDefaultAsync(s => s.OrderId == orderId && !s.IsDeleted, cancellationToken);

        if (shipment != null && !string.IsNullOrWhiteSpace(shipment.GhnOrderCode))
        {
            return MapToDto(shipment);
        }

        if (!_ghnService.IsConfigured())
        {
            _logger.LogWarning(
                "Skip GHN shipment creation for OrderId {OrderId} because GHN is not configured.",
                order.OrderId);

            if (shipment == null)
            {
                shipment = new Shipment
                {
                    OrderId = order.OrderId,
                    DeliveryStatus = "PendingConfig",
                    RawStatus = "pending_config",
                    ShippingFee = order.ShippingFee,
                    CodAmount = payment.CodAmount ?? 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.Shipments.AddAsync(shipment, cancellationToken);
            }
            else
            {
                shipment.DeliveryStatus = string.IsNullOrWhiteSpace(shipment.DeliveryStatus)
                    ? "PendingConfig"
                    : shipment.DeliveryStatus;
                shipment.RawStatus = string.IsNullOrWhiteSpace(shipment.RawStatus)
                    ? "pending_config"
                    : shipment.RawStatus;
                shipment.UpdatedAt = DateTime.UtcNow;
            }

            order.DeliveryStatus = shipment.DeliveryStatus;
            await _context.SaveChangesAsync(cancellationToken);
            return MapToDto(shipment);
        }

        var ghnRequest = BuildCreateOrderRequest(order, payment);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var ghnResponse = await _ghnService.CreateShippingOrderAsync(ghnRequest, cancellationToken);

        var previousStatus = shipment?.DeliveryStatus;

        if (shipment == null)
        {
            shipment = new Shipment
            {
                OrderId = order.OrderId,
                CreatedAt = DateTime.UtcNow
            };
            await _context.Shipments.AddAsync(shipment, cancellationToken);
        }

        var normalizedStatus = NormalizeDeliveryStatus(ghnResponse.RawStatus);
        shipment.GhnOrderCode = ghnResponse.OrderCode;
        shipment.ServiceId = ghnResponse.ServiceId;
        shipment.DeliveryStatus = normalizedStatus;
        shipment.RawStatus = ghnResponse.RawStatus;
        shipment.TrackingUrl = BuildTrackingUrl(ghnResponse.OrderCode);
        shipment.ShippingFee = order.ShippingFee;
        shipment.CodAmount = payment.CodAmount ?? 0;
        shipment.UpdatedAt = DateTime.UtcNow;

        order.DeliveryStatus = normalizedStatus;
        if (string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            order.Status = "Confirmed";
        }

        await _context.ShipmentStatusUpdates.AddAsync(new ShipmentStatusUpdate
        {
            ShipmentId = shipment.ShipmentId,
            PreviousStatus = previousStatus,
            NewStatus = normalizedStatus,
            RawStatus = ghnResponse.RawStatus,
            Note = $"Shipment created from {triggerSource}",
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await TryCreateDeliveryNotificationAsync(
            order.UserId,
            order.OrderNumber,
            normalizedStatus,
            $"Don hang {order.OrderNumber} da duoc tao van don va cho lay hang.");

        return MapToDto(shipment);
    }

    public async Task<ShipmentDto> SyncShipmentStatusFromGhnAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.OrderId == orderId && !o.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Order {orderId} not found.");

        var shipment = await _context.Shipments
            .FirstOrDefaultAsync(s => s.OrderId == orderId && !s.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Shipment for order {orderId} not found.");

        if (string.IsNullOrWhiteSpace(shipment.GhnOrderCode))
        {
            throw new InvalidOperationException("Shipment does not contain GHN order code.");
        }

        if (!_ghnService.IsConfigured())
        {
            throw new InvalidOperationException(
                "GHN is not configured. Please configure Ghn section in appsettings before syncing.");
        }

        var detail = await _ghnService.GetShippingOrderDetailAsync(shipment.GhnOrderCode, cancellationToken);
        var rawStatus = detail.Status?.Trim();
        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            throw new InvalidOperationException(
                $"GHN detail for order code '{shipment.GhnOrderCode}' does not contain status.");
        }

        var normalizedStatus = NormalizeDeliveryStatus(rawStatus);
        var previousStatus = shipment.DeliveryStatus;

        var statusChanged = !string.Equals(previousStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(shipment.RawStatus, rawStatus, StringComparison.OrdinalIgnoreCase);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        shipment.DeliveryStatus = normalizedStatus;
        shipment.RawStatus = rawStatus;
        shipment.UpdatedAt = detail.UpdatedDate ?? DateTime.UtcNow;

        if (detail.ServiceId.HasValue && detail.ServiceId.Value > 0)
        {
            shipment.ServiceId = detail.ServiceId;
        }

        if (detail.CodAmount.HasValue && detail.CodAmount.Value >= 0)
        {
            shipment.CodAmount = detail.CodAmount.Value;
        }

        order.DeliveryStatus = normalizedStatus;
        ApplyOrderStatusTransitionFromGhnSync(order, normalizedStatus);

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.OrderId == orderId && !p.IsDeleted, cancellationToken);

        if (payment != null)
        {
            if (IsCod(payment.PaymentMethod) && detail.CodAmount.HasValue && detail.CodAmount.Value >= 0)
            {
                payment.CodAmount = detail.CodAmount.Value;
            }

            if (IsCod(payment.PaymentMethod) && string.Equals(normalizedStatus, "Delivered", StringComparison.OrdinalIgnoreCase))
            {
                payment.PaymentStatus = "Paid";
                payment.PaidAt ??= DateTime.UtcNow;
                payment.CodAmount ??= shipment.CodAmount > 0 ? shipment.CodAmount : order.FinalAmount;
            }
            else if (IsCod(payment.PaymentMethod)
                     && !string.Equals(normalizedStatus, "Delivered", StringComparison.OrdinalIgnoreCase)
                     && string.Equals(payment.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                payment.PaymentStatus = "Pending";
                payment.PaidAt = null;
            }
        }

        if (statusChanged)
        {
            await _context.ShipmentStatusUpdates.AddAsync(new ShipmentStatusUpdate
            {
                ShipmentId = shipment.ShipmentId,
                PreviousStatus = previousStatus,
                NewStatus = normalizedStatus,
                RawStatus = rawStatus,
                Note = "Synchronized from GHN detail API.",
                UpdatedAt = detail.UpdatedDate ?? DateTime.UtcNow
            }, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (statusChanged)
        {
            await TryCreateDeliveryNotificationAsync(
                order.UserId,
                order.OrderNumber,
                normalizedStatus,
                BuildDeliveryNotificationMessage(
                    order.OrderNumber,
                    normalizedStatus,
                    "Dong bo trang thai tu GHN."));
        }

        return MapToDto(shipment);
    }

    public async Task ProcessGhnWebhookAsync(GhnWebhookRequest request, string? tokenHeader, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (!_ghnService.ValidateWebhookToken(tokenHeader))
        {
            throw new UnauthorizedAccessException("Invalid GHN webhook token.");
        }

        var shipment = await FindShipmentByWebhookPayloadAsync(request, cancellationToken)
            ?? throw new KeyNotFoundException("Shipment not found for webhook payload.");

        var normalizedStatus = NormalizeDeliveryStatus(request.Status);
        var rawStatus = request.Status?.Trim();

        if (string.Equals(shipment.RawStatus, rawStatus, StringComparison.OrdinalIgnoreCase)
            && string.Equals(shipment.DeliveryStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var previousStatus = shipment.DeliveryStatus;
        shipment.DeliveryStatus = normalizedStatus;
        shipment.RawStatus = rawStatus;
        shipment.UpdatedAt = DateTime.UtcNow;

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.OrderId == shipment.OrderId && !o.IsDeleted, cancellationToken);

        if (order != null)
        {
            order.DeliveryStatus = normalizedStatus;
            ApplyOrderStatusTransition(order, normalizedStatus);
        }

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.OrderId == shipment.OrderId && !p.IsDeleted, cancellationToken);

        if (payment != null && IsCod(payment.PaymentMethod) && string.Equals(normalizedStatus, "Delivered", StringComparison.OrdinalIgnoreCase))
        {
            payment.PaymentStatus = "Paid";
            payment.PaidAt ??= DateTime.UtcNow;
            payment.CodAmount ??= shipment.CodAmount > 0 ? shipment.CodAmount : order?.FinalAmount ?? 0;
        }

        await _context.ShipmentStatusUpdates.AddAsync(new ShipmentStatusUpdate
        {
            ShipmentId = shipment.ShipmentId,
            PreviousStatus = previousStatus,
            NewStatus = normalizedStatus,
            RawStatus = rawStatus,
            Note = request.Description,
            UpdatedAt = request.UpdatedDate ?? DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (order != null)
        {
            await TryCreateDeliveryNotificationAsync(
                order.UserId,
                order.OrderNumber,
                normalizedStatus,
                BuildDeliveryNotificationMessage(order.OrderNumber, normalizedStatus, request.Description));
        }
    }

    private async Task<Shipment?> FindShipmentByWebhookPayloadAsync(GhnWebhookRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.OrderCode))
        {
            var shipmentByCode = await _context.Shipments
                .FirstOrDefaultAsync(
                    s => !s.IsDeleted && s.GhnOrderCode != null && s.GhnOrderCode == request.OrderCode,
                    cancellationToken);
            if (shipmentByCode != null)
            {
                return shipmentByCode;
            }
        }

        if (string.IsNullOrWhiteSpace(request.ClientOrderCode))
        {
            return null;
        }

        var order = await _context.Orders
            .FirstOrDefaultAsync(
                o => !o.IsDeleted && o.OrderNumber == request.ClientOrderCode,
                cancellationToken);

        if (order == null)
        {
            return null;
        }

        return await _context.Shipments
            .FirstOrDefaultAsync(s => s.OrderId == order.OrderId && !s.IsDeleted, cancellationToken);
    }

    private GhnCreateOrderRequest BuildCreateOrderRequest(Order order, Payment payment)
    {
        var shippingAddress = SanitizeShippingAddress(order.ShippingAddress, order.RecipientName, order.RecipientPhone);
        if (string.IsNullOrWhiteSpace(shippingAddress))
        {
            throw new InvalidOperationException("Order is missing shipping address.");
        }

        if (!int.TryParse(order.DistrictCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var districtId)
            || districtId <= 0)
        {
            throw new InvalidOperationException("Order district code is missing or invalid for GHN.");
        }

        if (string.IsNullOrWhiteSpace(order.WardCode))
        {
            throw new InvalidOperationException("Order ward code is required for GHN.");
        }

        var recipientPhone = order.RecipientPhone?.Trim();
        if (string.IsNullOrWhiteSpace(recipientPhone))
        {
            throw new InvalidOperationException("Recipient phone is required for GHN shipment creation.");
        }

        var defaultItemWeight = GetOptionalInt("Ghn:DefaultItemWeight") ?? 200;
        var totalQuantity = order.OrderDetails.Sum(d => Math.Max(1, d.Quantity));
        var weight = Math.Max(GetOptionalInt("Ghn:DefaultWeight") ?? 1000, totalQuantity * defaultItemWeight);
        var length = GetOptionalInt("Ghn:DefaultLength") ?? 20;
        var width = GetOptionalInt("Ghn:DefaultWidth") ?? 20;
        var height = GetOptionalInt("Ghn:DefaultHeight") ?? 10;

        return new GhnCreateOrderRequest
        {
            OrderId = order.OrderId,
            OrderNumber = order.OrderNumber,
            ToName = order.RecipientName?.Trim()
                ?? order.User?.DisplayName?.Trim()
                ?? "Khach hang NongXanh",
            ToPhone = recipientPhone,
            ToAddress = shippingAddress,
            ToDistrictId = districtId,
            ToWardCode = order.WardCode,
            InsuranceValue = order.FinalAmount,
            CodAmount = IsCod(payment.PaymentMethod) ? order.FinalAmount : 0,
            Content = $"Don hang {order.OrderNumber}",
            Weight = weight,
            Length = length,
            Width = width,
            Height = height,
            ServiceTypeId = GetOptionalInt("Ghn:DefaultServiceTypeId") ?? 2,
            Items = order.OrderDetails.Select(d => new GhnCreateOrderItemDto
            {
                Name = BuildItemName(d),
                Quantity = Math.Max(1, d.Quantity),
                Price = d.Price,
                Weight = defaultItemWeight
            }).ToList()
        };
    }

    private static string BuildItemName(OrderDetail detail)
    {
        var productName = detail.ProductVariant?.Product?.ProductName;
        var variantName = detail.ProductVariant?.VariantName;

        if (!string.IsNullOrWhiteSpace(productName) && !string.IsNullOrWhiteSpace(variantName))
        {
            return $"{productName} - {variantName}";
        }

        return !string.IsNullOrWhiteSpace(productName)
            ? productName
            : variantName ?? "San pham";
    }

    private static string? SanitizeShippingAddress(string? shippingAddress, string? recipientName, string? recipientPhone)
    {
        if (string.IsNullOrWhiteSpace(shippingAddress))
        {
            return null;
        }

        var parts = shippingAddress
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (parts.Count == 0)
        {
            return shippingAddress.Trim();
        }

        var hasNamePrefix = !string.IsNullOrWhiteSpace(recipientName)
            && parts[0].Equals(recipientName.Trim(), StringComparison.OrdinalIgnoreCase);

        var phoneIndex = hasNamePrefix ? 1 : 0;
        var hasPhonePrefix = parts.Count > phoneIndex
            && IsSamePhone(parts[phoneIndex], recipientPhone);

        if (hasNamePrefix && hasPhonePrefix && parts.Count > 2)
        {
            return string.Join(", ", parts.Skip(2));
        }

        if (!hasNamePrefix && hasPhonePrefix && parts.Count > 1)
        {
            return string.Join(", ", parts.Skip(1));
        }

        if (parts.Count > 2 && !LooksLikePhonePart(parts[0]) && LooksLikePhonePart(parts[1]))
        {
            return string.Join(", ", parts.Skip(2));
        }

        if (parts.Count > 1 && LooksLikePhonePart(parts[0]))
        {
            return string.Join(", ", parts.Skip(1));
        }

        return shippingAddress.Trim();
    }

    private static bool IsSamePhone(string currentPart, string? expected)
    {
        var left = NormalizePhoneNumber(currentPart);
        var right = NormalizePhoneNumber(expected);

        return !string.IsNullOrEmpty(right)
            && left.Equals(right, StringComparison.Ordinal);
    }

    private static bool LooksLikePhonePart(string value)
    {
        var digits = NormalizePhoneNumber(value);
        return digits.Length >= 9 && digits.Length <= 15;
    }

    private static string NormalizePhoneNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static void ApplyOrderStatusTransition(Order order, string deliveryStatus)
    {
        if (string.Equals(deliveryStatus, "Delivered", StringComparison.OrdinalIgnoreCase))
        {
            order.Status = "Delivered";
            return;
        }

        if (string.Equals(deliveryStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(order.Status, "Delivered", StringComparison.OrdinalIgnoreCase))
            {
                order.Status = "Cancelled";
            }
            return;
        }

        if (string.Equals(deliveryStatus, "Returned", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(order.Status, "Delivered", StringComparison.OrdinalIgnoreCase))
            {
                order.Status = "Returned";
            }
            return;
        }

        if (deliveryStatus is "ReadyToPick" or "Picking" or "Storing" or "InTransit" or "OutForDelivery")
        {
            if (!string.Equals(order.Status, "Delivered", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(order.Status, "Returned", StringComparison.OrdinalIgnoreCase))
            {
                order.Status = "Shipping";
            }
        }
    }

    private static void ApplyOrderStatusTransitionFromGhnSync(Order order, string deliveryStatus)
    {
        if (string.Equals(deliveryStatus, "Delivered", StringComparison.OrdinalIgnoreCase))
        {
            order.Status = "Delivered";
            return;
        }

        if (string.Equals(deliveryStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            order.Status = "Cancelled";
            return;
        }

        if (string.Equals(deliveryStatus, "Returned", StringComparison.OrdinalIgnoreCase))
        {
            order.Status = "Returned";
            return;
        }

        if (string.Equals(deliveryStatus, "ShipmentCreated", StringComparison.OrdinalIgnoreCase))
        {
            order.Status = "Confirmed";
            return;
        }

        if (deliveryStatus is "ReadyToPick" or "Picking" or "Storing" or "InTransit" or "OutForDelivery")
        {
            order.Status = "Shipping";
        }
    }

    private static string NormalizeDeliveryStatus(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            return "ShipmentCreated";
        }

        var value = rawStatus.Trim().ToLowerInvariant();

        return value switch
        {
            "ready_to_pick" => "ReadyToPick",
            "picking" or "money_collect_picking" or "picked" => "Picking",
            "storing" or "sorting" => "Storing",
            "transporting" => "InTransit",
            "delivering" or "money_collect_delivering" => "OutForDelivery",
            "delivered" => "Delivered",
            "cancel" or "cancelled" => "Cancelled",
            "return" or "returned" or "return_transporting" => "Returned",
            "delivery_fail" or "waiting_to_return" => "DeliveryFailed",
            _ => "ShipmentCreated"
        };
    }

    private string? BuildTrackingUrl(string? orderCode)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
        {
            return null;
        }

        var template = _configuration["Ghn:TrackingUrlTemplate"]?.Trim();
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        return template.Contains("{orderCode}", StringComparison.Ordinal)
            ? template.Replace("{orderCode}", orderCode, StringComparison.Ordinal)
            : template;
    }

    private static bool IsCod(string? paymentMethod)
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

    private static bool IsVnPay(string? paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            return false;
        }

        var method = paymentMethod.Trim();
        return method.Equals("VNPay", StringComparison.OrdinalIgnoreCase)
            || method.Equals("VNPAY", StringComparison.OrdinalIgnoreCase);
    }

    private int? GetOptionalInt(string key)
    {
        var value = _configuration[key]?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private async Task TryCreateDeliveryNotificationAsync(
        Guid userId,
        string orderNumber,
        string status,
        string content)
    {
        try
        {
            await _notificationService.CreateAsync(new CreateNotificationRequest
            {
                UserId = userId,
                Title = $"Cap nhat giao hang don {orderNumber}",
                Content = content,
                Type = $"DELIVERY_{status.ToUpperInvariant()}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to create delivery notification for OrderNumber {OrderNumber}",
                orderNumber);
        }
    }

    private static string BuildDeliveryNotificationMessage(string orderNumber, string status, string? description)
    {
        var fallback = status switch
        {
            "ReadyToPick" => "Don hang dang cho lay hang.",
            "Picking" => "Don hang dang duoc lay hang.",
            "Storing" => "Don hang dang o kho trung chuyen.",
            "InTransit" => "Don hang dang tren duong van chuyen.",
            "OutForDelivery" => "Don hang dang duoc giao den ban.",
            "Delivered" => "Don hang da giao thanh cong.",
            "Cancelled" => "Don hang da bi huy van don.",
            "Returned" => "Don hang da duoc hoan tra.",
            _ => "Don hang co cap nhat moi."
        };

        return string.IsNullOrWhiteSpace(description)
            ? $"Don {orderNumber}: {fallback}"
            : $"Don {orderNumber}: {description}";
    }

    private static ShipmentDto MapToDto(Shipment shipment)
    {
        return new ShipmentDto
        {
            ShipmentId = shipment.ShipmentId,
            OrderId = shipment.OrderId,
            GhnOrderCode = shipment.GhnOrderCode,
            ServiceId = shipment.ServiceId,
            DeliveryStatus = shipment.DeliveryStatus,
            RawStatus = shipment.RawStatus,
            TrackingUrl = shipment.TrackingUrl,
            ShippingFee = shipment.ShippingFee,
            CodAmount = shipment.CodAmount,
            CreatedAt = shipment.CreatedAt,
            UpdatedAt = shipment.UpdatedAt
        };
    }
}
