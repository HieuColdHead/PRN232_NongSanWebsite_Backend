using BLL.DTOs;
using BLL.DTOs.Ghn;
using BLL.Services.Interfaces;
using System.Globalization;
using DAL.Data;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using BLL.Hubs;

namespace BLL.Services;

public class OrderService : IOrderService
{
    private readonly IGenericRepository<Order> _orderRepository;
    private readonly IGenericRepository<ProductVariant> _variantRepository;
    private readonly IGenericRepository<MealCombo> _mealComboRepository;
    private readonly ApplicationDbContext _context;
    private readonly IPaymentService _paymentService;
    private readonly IGhnService _ghnService;
    private readonly IShipmentService _shipmentService;
    private readonly INotificationService _notificationService;
    private readonly IHubContext<AppHub> _hubContext;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IGenericRepository<Order> orderRepository,
        IGenericRepository<ProductVariant> variantRepository,
        IGenericRepository<MealCombo> mealComboRepository,
        ApplicationDbContext context,
        IPaymentService paymentService,
        IGhnService ghnService,
        IShipmentService shipmentService,
        INotificationService notificationService,
        IHubContext<AppHub> hubContext,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _variantRepository = variantRepository;
        _mealComboRepository = mealComboRepository;
        _context = context;
        _paymentService = paymentService;
        _ghnService = ghnService;
        _shipmentService = shipmentService;
        _notificationService = notificationService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<PagedResult<OrderDto>> GetPagedAsync(int pageNumber, int pageSize)
    {
        var all = await _orderRepository.GetAllAsync();
        var totalCount = all.Count();
        var items = all
            .OrderByDescending(o => o.OrderDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);

        var dtos = new List<OrderDto>();
        foreach (var order in items)
        {
            dtos.Add(await MapToDto(order));
        }

        return new PagedResult<OrderDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<OrderDto>> GetByUserIdAsync(Guid userId, int pageNumber, int pageSize)
    {
        var all = await _orderRepository.FindAsync(o => o.UserId == userId);
        var totalCount = all.Count();
        var items = all
            .OrderByDescending(o => o.OrderDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);

        var dtos = new List<OrderDto>();
        foreach (var order in items)
        {
            dtos.Add(await MapToDto(order));
        }

        return new PagedResult<OrderDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<OrderDto?> GetByIdAsync(Guid id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null) return null;
        return await MapToDto(order);
    }

    public async Task<OrderDto> ConfirmOrderAsync(Guid id)
    {
        var order = await _orderRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Order {id} not found.");

        if (!string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only pending orders can be confirmed.");
        }

        order.Status = "Confirmed";

        await _orderRepository.UpdateAsync(order);
        await _orderRepository.SaveChangesAsync();

        return await MapToDto(order);
    }

    public async Task<OrderDto> CancelOrderAsync(Guid id)
    {
        var order = await _orderRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Order {id} not found.");

        var isPending = string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase);
        var isConfirmed = string.Equals(order.Status, "Confirmed", StringComparison.OrdinalIgnoreCase);
        if (!isPending && !isConfirmed)
        {
            throw new InvalidOperationException("Only pending or confirmed orders can be cancelled.");
        }

        var hasShipment = await _context.Shipments
            .AsNoTracking()
            .AnyAsync(s => s.OrderId == id && !s.IsDeleted);
        if (hasShipment)
        {
            throw new InvalidOperationException("Cannot cancel order because shipment has already been created.");
        }

        order.Status = "Cancelled";
        order.DeliveryStatus = "Cancelled";

        await _orderRepository.UpdateAsync(order);
        await _orderRepository.SaveChangesAsync();

        return await MapToDto(order);
    }

    public async Task<OrderDto> CreateAsync(CreateOrderRequest request)
    {
        var sanitizedShippingAddress = SanitizeShippingAddress(request.ShippingAddress, recipientName: null, recipientPhone: null);

        var order = new Order
        {
            OrderDate = DateTime.UtcNow,
            ShippingFee = request.ShippingFee,
            ShippingAddress = sanitizedShippingAddress,
            ProvinceId = request.ProvinceId,
            UserId = request.UserId,
            Status = "Pending",
            VnPayStatus = "Pending",
            DeliveryStatus = "PendingShipment"
        };

        decimal totalAmount = 0;
        var details = new List<OrderDetail>();

        foreach (var item in request.OrderDetails)
        {
            decimal price = 0;
            if (item.VariantId.HasValue)
            {
                var variant = await _variantRepository.GetByIdAsync(item.VariantId.Value);
                price = variant?.Price ?? 0;
            }
            else if (item.MealComboId.HasValue)
            {
                var combo = await _mealComboRepository.GetByIdAsync(item.MealComboId.Value);
                price = combo?.BasePrice ?? 0;
            }

            var subTotal = price * item.Quantity;
            totalAmount += subTotal;

            details.Add(new OrderDetail
            {
                VariantId = item.VariantId,
                MealComboId = item.MealComboId,
                Quantity = item.Quantity,
                Price = price,
                SubTotal = subTotal
            });
        }

        order.TotalAmount = totalAmount;
        order.DiscountAmount = 0;
        order.FinalAmount = totalAmount + request.ShippingFee - order.DiscountAmount;
        order.OrderDetails = details;

        await _orderRepository.AddAsync(order);
        await _orderRepository.SaveChangesAsync();

        return await MapToDto(order);
    }

    public async Task<CheckoutPreviewDto> PreviewCheckoutAsync(Guid userId, CheckoutPreviewRequest request)
    {
        ValidateCartItemSelection(request.CartItemIds);
        ValidateGhnDestination(request.ToWardCode);

        var (_, selectedItems) = await LoadSelectedCartItemsAsync(userId, request.CartItemIds, asNoTracking: true);
        var (orderDetails, checkoutItems, totalAmount) = BuildOrderDetails(selectedItems);
        _ = orderDetails;

        var destinationDistrictId = await ResolveDestinationDistrictIdAsync(
            request.ToWardCode!,
            request.ProvinceId);

        var shippingFee = await ResolveShippingFeeAsync(
            destinationDistrictId,
            request.ToWardCode!,
            totalAmount,
            selectedItems,
            request.ProvinceId,
            request.InsuranceValue);

        var preDiscountTotal = totalAmount + shippingFee;
        var (_, discountAmount, appliedVoucherCode) = await ResolveVoucherAsync(
            userId,
            request.VoucherCode,
            preDiscountTotal,
            forUpdate: false);

        return new CheckoutPreviewDto
        {
            Items = checkoutItems,
            TotalAmount = totalAmount,
            ShippingFee = shippingFee,
            DiscountAmount = discountAmount,
            FinalAmount = Math.Max(0, preDiscountTotal - discountAmount),
            VoucherCode = appliedVoucherCode
        };
    }

    public async Task<CheckoutOrderResultDto> CheckoutFromCartAsync(Guid userId, CheckoutOrderRequest request)
    {
        ValidateCartItemSelection(request.CartItemIds);
        if (string.IsNullOrWhiteSpace(request.ShippingAddress))
        {
            throw new ArgumentException("Shipping address is required.", nameof(request.ShippingAddress));
        }

        if (string.IsNullOrWhiteSpace(request.ShippingMethod))
        {
            throw new ArgumentException("Shipping method is required.", nameof(request.ShippingMethod));
        }

        ValidateGhnDestination(request.ToWardCode);

        if (string.IsNullOrWhiteSpace(request.RecipientName))
        {
            throw new ArgumentException("Recipient name is required.", nameof(request.RecipientName));
        }

        if (string.IsNullOrWhiteSpace(request.RecipientPhone))
        {
            throw new ArgumentException("Recipient phone is required.", nameof(request.RecipientPhone));
        }

        var normalizedPaymentMethod = NormalizePaymentMethod(request.PaymentMethod);
        var sanitizedShippingAddress = SanitizeShippingAddress(
            request.ShippingAddress,
            request.RecipientName,
            request.RecipientPhone);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        Order order;
        PaymentDto payment;

        try
        {
            var (cart, selectedItems) = await LoadSelectedCartItemsAsync(userId, request.CartItemIds, asNoTracking: false);
            var (orderDetails, _, totalAmount) = BuildOrderDetails(selectedItems);

            var destinationDistrictId = await ResolveDestinationDistrictIdAsync(
                request.ToWardCode!,
                request.ProvinceId);

            var shippingFee = await ResolveShippingFeeAsync(
                destinationDistrictId,
                request.ToWardCode!,
                totalAmount,
                selectedItems,
                request.ProvinceId,
                request.InsuranceValue);

            var preDiscountTotal = totalAmount + shippingFee;
            var (voucher, discountAmount, _) = await ResolveVoucherAsync(
                userId,
                request.VoucherCode,
                preDiscountTotal,
                forUpdate: true);

            if (voucher != null)
            {
                ConsumeVoucher(voucher, userId);
            }

            var finalAmount = Math.Max(0, preDiscountTotal - discountAmount);
            order = new Order
            {
                OrderDate = DateTime.UtcNow,
                ShippingFee = shippingFee,
                ShippingAddress = sanitizedShippingAddress,
                UserId = userId,
                RecipientName = request.RecipientName?.Trim(),
                RecipientPhone = request.RecipientPhone?.Trim(),
                ProvinceCode = request.ProvinceCode?.Trim(),
                ProvinceId = request.ProvinceId,
                WardCode = request.ToWardCode?.Trim(),
                DeliveryStatus = "PendingShipment",
                Status = "Pending",
                VnPayStatus = normalizedPaymentMethod.Equals("VNPay", StringComparison.OrdinalIgnoreCase) ? "Pending" : "NotApplicable",
                TotalAmount = totalAmount,
                DiscountAmount = discountAmount,
                FinalAmount = finalAmount,
                OrderDetails = orderDetails
            };

            await _orderRepository.AddAsync(order);

            _context.CartItems.RemoveRange(selectedItems);
            var selectedIds = selectedItems.Select(i => i.CartItemId).ToList();
            var remainingTotal = await _context.CartItems
                .Where(ci => ci.CartId == cart.CartId && !selectedIds.Contains(ci.CartItemId))
                .SumAsync(ci => (decimal?)ci.SubTotal);
            cart.TotalAmount = remainingTotal ?? 0m;

            await _orderRepository.SaveChangesAsync();

            payment = await _paymentService.CreateAsync(new CreatePaymentRequest
            {
                PaymentMethod = normalizedPaymentMethod,
                OrderId = order.OrderId,
                CodAmount = normalizedPaymentMethod.Equals("COD", StringComparison.OrdinalIgnoreCase) ? finalAmount : 0
            });

            await transaction.CommitAsync();

            // Notifications
            await _notificationService.CreateAsync(new CreateNotificationRequest
            {
                UserId = userId,
                Title = "Đặt hàng thành công",
                Content = $"Đơn hàng {order.OrderNumber} của bạn đã được tạo thành công.",
                Type = "OrderSuccess"
            });

            // Notify Support (Admin & Staff) - Persistent and Real-time
            await _notificationService.CreateForSupportAsync(new CreateNotificationRequest
            {
                Title = "Đơn hàng mới",
                Content = $"Có đơn hàng mới {order.OrderNumber} từ khách hàng.",
                Type = "NewOrder"
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return new CheckoutOrderResultDto
        {
            Order = await MapToDto(order),
            Payment = payment,
            Shipment = null
        };
    }

    public async Task UpdateAsync(Guid id, UpdateOrderRequest request)
    {
        var order = await _orderRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Order {id} not found");

        if (request.ShippingAddress != null)
            order.ShippingAddress = SanitizeShippingAddress(request.ShippingAddress, order.RecipientName, order.RecipientPhone);
        if (request.ProvinceId.HasValue)
            order.ProvinceId = request.ProvinceId;
        if (request.Status != null)
        {
            var oldStatus = order.Status;
            order.Status = request.Status;

            if (!string.Equals(oldStatus, request.Status, StringComparison.OrdinalIgnoreCase))
            {
                await _notificationService.CreateAsync(new CreateNotificationRequest
                {
                    UserId = order.UserId,
                    Title = "Cập nhật trạng thái đơn hàng",
                    Content = $"Đơn hàng {order.OrderNumber} của bạn đã chuyển sang trạng thái: {request.Status}.",
                    Type = "OrderStatusUpdate"
                });
            }
        }
        if (request.VnPayStatus != null)
            order.VnPayStatus = request.VnPayStatus;

        await _orderRepository.UpdateAsync(order);
        await _orderRepository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _orderRepository.DeleteAsync(id);
        await _orderRepository.SaveChangesAsync();
    }

    private static void ValidateCartItemSelection(IEnumerable<Guid> cartItemIds)
    {
        if (cartItemIds == null || !cartItemIds.Any())
        {
            throw new ArgumentException("Please select at least one cart item.");
        }
    }

    private static string NormalizePaymentMethod(string? paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            throw new ArgumentException("Payment method is required.", nameof(paymentMethod));
        }

        var value = paymentMethod.Trim();

        if (value.Equals("COD", StringComparison.OrdinalIgnoreCase)
            || value.Equals("CashOnDelivery", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Cash On Delivery", StringComparison.OrdinalIgnoreCase))
        {
            return "COD";
        }

        if (value.Equals("VNPay", StringComparison.OrdinalIgnoreCase)
            || value.Equals("VNPAY", StringComparison.OrdinalIgnoreCase))
        {
            return "VNPay";
        }

        throw new ArgumentException("Unsupported payment method. Allowed values: COD, VNPay.", nameof(paymentMethod));
    }

    private static string SanitizeShippingAddress(string? shippingAddress, string? recipientName, string? recipientPhone)
    {
        if (string.IsNullOrWhiteSpace(shippingAddress))
        {
            return string.Empty;
        }

        var parts = shippingAddress
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (parts.Count == 0)
        {
            return shippingAddress.Trim();
        }

        var hasNamePrefix = parts.Count > 0 && IsSameText(parts[0], recipientName);
        var phoneIndex = hasNamePrefix ? 1 : 0;
        var hasPhonePrefix = parts.Count > phoneIndex && IsSamePhone(parts[phoneIndex], recipientPhone);

        if (hasNamePrefix && hasPhonePrefix && parts.Count > 2)
        {
            return string.Join(", ", parts.Skip(2));
        }

        if (!hasNamePrefix && hasPhonePrefix && parts.Count > 1)
        {
            return string.Join(", ", parts.Skip(1));
        }

        // Fallback for flows that do not send recipient fields (or old rows with mismatched data):
        // if address starts with "Name, Phone, ..." then keep only the real address part.
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

    private static bool IsSameText(string currentPart, string? expected)
    {
        return !string.IsNullOrWhiteSpace(expected)
            && currentPart.Trim().Equals(expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSamePhone(string currentPart, string? expected)
    {
        var left = NormalizePhoneNumber(currentPart);
        var right = NormalizePhoneNumber(expected);

        return !string.IsNullOrEmpty(right)
            && left.Equals(right, StringComparison.Ordinal);
    }

    private static string NormalizePhoneNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static bool LooksLikePhonePart(string value)
    {
        var digits = NormalizePhoneNumber(value);
        return digits.Length >= 9 && digits.Length <= 15;
    }

    private static void ValidateGhnDestination(string? toWardCode)
    {
        if (string.IsNullOrWhiteSpace(toWardCode))
        {
            throw new ArgumentException("Destination ward (toWardCode) is required for GHN shipping fee.", nameof(toWardCode));
        }
    }

    private async Task<int> ResolveDestinationDistrictIdAsync(string toWardCode, int? provinceId)
    {
        if (!provinceId.HasValue || provinceId.Value <= 0)
        {
            throw new ArgumentException(
                "provinceId is required.",
                nameof(provinceId));
        }

        if (!_ghnService.IsConfigured())
        {
            throw new InvalidOperationException(
                "GHN is not configured. Please configure Ghn section in appsettings before checkout.");
        }

        try
        {
            return await _ghnService.ResolveDistrictIdByWardAsync(toWardCode.Trim(), provinceId);
        }
        catch (InvalidOperationException)
        {
            throw new ArgumentException(
                "Invalid wardCode for the selected province. Please reload wards and choose again.",
                nameof(toWardCode));
        }
    }

    private async Task<decimal> ResolveShippingFeeAsync(
        int toDistrictId,
        string toWardCode,
        decimal insuranceValue,
        List<CartItem> selectedItems,
        int? provinceId,
        decimal overrideInsuranceValue)
    {
        if (!_ghnService.IsConfigured())
        {
            throw new InvalidOperationException(
                "GHN is not configured. Please configure Ghn section in appsettings before checkout.");
        }

        var totalQuantity = selectedItems.Sum(i => Math.Max(1, i.Quantity));
        var estimatedWeight = Math.Max(1000, totalQuantity * 200);

        return await _ghnService.CalculateShippingFeeAsync(new GhnCalculateFeeRequest
        {
            ToDistrictId = toDistrictId,
            ToWardCode = toWardCode.Trim(),
            ProvinceId = provinceId,
            InsuranceValue = Math.Max(0, overrideInsuranceValue > 0 ? overrideInsuranceValue : insuranceValue),
            Weight = estimatedWeight,
            Length = 20,
            Width = 20,
            Height = 10,
            ServiceTypeId = null
        });
    }

    private async Task<(Cart Cart, List<CartItem> Items)> LoadSelectedCartItemsAsync(
        Guid userId,
        IEnumerable<Guid> cartItemIds,
        bool asNoTracking)
    {
        var selectedIds = cartItemIds.Distinct().ToList();
        var cart = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Status == "Active" && !c.IsDeleted)
            ?? throw new KeyNotFoundException("Active cart not found.");

        IQueryable<CartItem> query = _context.CartItems
            .Where(ci => ci.CartId == cart.CartId && selectedIds.Contains(ci.CartItemId))
            .Include(ci => ci.MealCombo)
            .Include(ci => ci.ProductVariant!)
                .ThenInclude(v => v.Product);

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        var items = await query.ToListAsync();

        if (items.Count != selectedIds.Count)
        {
            throw new KeyNotFoundException("Some selected cart items were not found in your active cart.");
        }

        if (items.Any(i => i.Quantity <= 0))
        {
            throw new InvalidOperationException("Selected cart items must have quantity greater than 0.");
        }

        var hasInvalidVariant = items.Any(i =>
            i.VariantId.HasValue
            && (i.ProductVariant == null || i.ProductVariant.IsDeleted));

        if (hasInvalidVariant)
        {
            throw new InvalidOperationException("Some selected products are no longer available.");
        }

        var hasInvalidCombo = items.Any(i =>
            i.MealComboId.HasValue
            && (i.MealCombo == null || !i.MealCombo.IsActive));

        if (hasInvalidCombo)
        {
            throw new InvalidOperationException("Some selected meal combos are no longer available.");
        }

        if (items.Any(i => !i.VariantId.HasValue && !i.MealComboId.HasValue))
        {
            throw new InvalidOperationException("Some selected cart items are invalid.");
        }

        return (cart, items);
    }

    private static (List<OrderDetail> OrderDetails, List<CheckoutItemDto> CheckoutItems, decimal TotalAmount) BuildOrderDetails(List<CartItem> selectedItems)
    {
        decimal totalAmount = 0;
        var orderDetails = new List<OrderDetail>();
        var checkoutItems = new List<CheckoutItemDto>();

        foreach (var item in selectedItems)
        {
            var unitPrice = item.ProductVariant?.Price ?? item.MealCombo?.BasePrice ?? item.PriceAtTime;
            var subTotal = unitPrice * item.Quantity;
            totalAmount += subTotal;

            orderDetails.Add(new OrderDetail
            {
                VariantId = item.VariantId,
                MealComboId = item.MealComboId,
                Quantity = item.Quantity,
                Price = unitPrice,
                SubTotal = subTotal
            });

            checkoutItems.Add(new CheckoutItemDto
            {
                CartItemId = item.CartItemId,
                VariantId = item.VariantId,
                MealComboId = item.MealComboId,
                ProductName = item.MealCombo?.Name ?? item.ProductVariant?.Product?.ProductName,
                VariantName = item.ProductVariant?.VariantName, // This will be null for combo
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                SubTotal = subTotal
            });
        }

        return (orderDetails, checkoutItems, totalAmount);
    }

    private async Task<(Voucher? Voucher, decimal DiscountAmount, string? AppliedVoucherCode)> ResolveVoucherAsync(
        Guid userId,
        string? voucherCode,
        decimal totalAmount,
        bool forUpdate)
    {
        if (string.IsNullOrWhiteSpace(voucherCode))
        {
            return (null, 0, null);
        }

        var normalizedCode = voucherCode.Trim();
        var voucherQuery = _context.Vouchers
            .Where(v => !v.IsDeleted && v.Code != null && v.Code.ToLower() == normalizedCode.ToLower());

        if (!forUpdate)
        {
            voucherQuery = voucherQuery.AsNoTracking();
        }

        var voucher = await voucherQuery.FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Voucher not found.");

        ValidateVoucher(voucher, totalAmount);

        var alreadyUsed = await _context.UserVouchers
            .AnyAsync(uv => uv.UserId == userId && uv.VoucherId == voucher.VoucherId && uv.IsUsed);

        if (alreadyUsed)
        {
            throw new InvalidOperationException("Voucher has already been used by this user.");
        }

        var discount = CalculateDiscount(voucher, totalAmount);
        return (voucher, discount, voucher.Code);
    }

    private static void ValidateVoucher(Voucher voucher, decimal totalAmount)
    {
        if (!string.Equals(voucher.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Voucher is not active.");
        }

        var now = DateTime.UtcNow;
        if (voucher.StartDate.HasValue && voucher.StartDate.Value > now)
        {
            throw new InvalidOperationException("Voucher has not started yet.");
        }

        if (voucher.EndDate.HasValue && voucher.EndDate.Value < now)
        {
            throw new InvalidOperationException("Voucher has expired.");
        }

        if (voucher.Quantity <= 0)
        {
            throw new InvalidOperationException("Voucher is out of stock.");
        }

        if (totalAmount < voucher.MinOrderValue)
        {
            throw new InvalidOperationException($"Order total must be at least {voucher.MinOrderValue} to use this voucher.");
        }
    }

    private static decimal CalculateDiscount(Voucher voucher, decimal totalAmount)
    {
        var discountType = voucher.DiscountType?.Trim().ToLowerInvariant();
        decimal discount = discountType switch
        {
            "percent" or "percentage" => totalAmount * voucher.DiscountValue / 100m,
            "fixed" or "amount" => voucher.DiscountValue,
            _ => throw new InvalidOperationException("Unsupported voucher discount type.")
        };

        if (voucher.MaxDiscount > 0)
        {
            discount = Math.Min(discount, voucher.MaxDiscount);
        }

        return Math.Min(totalAmount, Math.Max(discount, 0));
    }

    private void ConsumeVoucher(Voucher voucher, Guid userId)
    {
        voucher.Quantity = Math.Max(0, voucher.Quantity - 1);
        if (voucher.Quantity == 0 && string.Equals(voucher.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            voucher.Status = "Inactive";
        }

        _context.UserVouchers.Add(new UserVoucher
        {
            UserId = userId,
            VoucherId = voucher.VoucherId,
            IsUsed = true,
            UsedAt = DateTime.UtcNow
        });
    }

    private async Task<OrderDto> MapToDto(Order order)
    {
        var customer = order.User;
        if (customer == null)
        {
            customer = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == order.UserId);
        }

        // Load order details with ProductVariant -> Product -> ProductImages
        // and MealCombo -> Items -> Product -> ProductImages (for display)
        var orderDetails = await _context.Set<OrderDetail>()
            .Where(d => d.OrderId == order.OrderId)
            .Include(d => d.ProductVariant!)
                .ThenInclude(v => v.Product!)
                    .ThenInclude(p => p.ProductImages)
            .Include(d => d.MealCombo!)
                .ThenInclude(mc => mc.Items)
                    .ThenInclude(i => i.Product!)
                        .ThenInclude(p => p.ProductImages)
            .Include(d => d.MealCombo!)
                .ThenInclude(mc => mc.Items)
                    .ThenInclude(i => i.SuggestedVariant)
            .ToListAsync();

        var shipment = await _context.Shipments
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrderId == order.OrderId && !s.IsDeleted);

        var payment = await _context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrderId == order.OrderId && !p.IsDeleted);

        var sanitizedShippingAddress = SanitizeShippingAddress(order.ShippingAddress, order.RecipientName, order.RecipientPhone);

        return new OrderDto
        {
            OrderId = order.OrderId,
            OrderNumber = order.OrderNumber,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            ShippingFee = order.ShippingFee,
            DiscountAmount = order.DiscountAmount,
            FinalAmount = order.FinalAmount,
            ShippingAddress = sanitizedShippingAddress,
            Status = order.Status,
            PaymentMethod = payment?.PaymentMethod,
            VnPayStatus = order.VnPayStatus,
            DeliveryStatus = order.DeliveryStatus,
            RecipientName = order.RecipientName,
            RecipientPhone = order.RecipientPhone,
            ProvinceCode = order.ProvinceCode,
            ProvinceId = order.ProvinceId,
            WardCode = order.WardCode,
            UserId = order.UserId,
            CustomerDisplayName = customer?.DisplayName,
            CustomerEmail = customer?.Email,
            CustomerPhoneNumber = customer?.PhoneNumber,
            Shipment = shipment == null
                ? null
                : new ShipmentDto
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
                },
            OrderDetails = orderDetails.Select(d => new OrderDetailDto
            {
                OrderDetailId = d.OrderDetailId,
                Quantity = d.Quantity,
                Price = d.Price,
                SubTotal = d.SubTotal,
                OrderId = d.OrderId,
                VariantId = d.VariantId,
                VariantName = d.ProductVariant?.VariantName,
                MealComboId = d.MealComboId,
                MealComboName = d.MealCombo?.Name,
                ComboItems = d.MealComboId.HasValue
                    ? d.MealCombo?.Items.Select(i => new MealComboOrderItemDto
                    {
                        ProductId = i.ProductId,
                        ProductName = i.Product?.ProductName,
                        VariantId = i.SuggestedVariantId,
                        VariantName = i.SuggestedVariant?.VariantName,
                        Quantity = i.Quantity,
                        Unit = i.Unit ?? i.Product?.Unit,
                        UnitPrice = i.SuggestedUnitPrice,
                        LineTotal = Decimal.Round(i.SuggestedUnitPrice * i.Quantity, 0),
                        ImageUrl = i.Product?.ProductImages
                            ?.FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                            ?? i.Product?.ProductImages?.FirstOrDefault()?.ImageUrl,
                        Origin = i.Product?.Origin
                    }).ToList()
                    : null,
                ProductId = d.ProductVariant?.ProductId ?? Guid.Empty,
                ProductName = d.ProductVariant?.Product?.ProductName,
                ProductImageUrl = d.ProductVariant?.Product?.ProductImages
                    ?.FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                    ?? d.ProductVariant?.Product?.ProductImages?.FirstOrDefault()?.ImageUrl,
                Unit = d.ProductVariant?.Product?.Unit,
                Origin = d.ProductVariant?.Product?.Origin
            }).ToList()
        };
    }
}
