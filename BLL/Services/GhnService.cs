using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;
using BLL.DTOs.Ghn;
using BLL.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BLL.Services;

public sealed class GhnService : IGhnService
{
    private static readonly ConcurrentDictionary<string, int> WardDistrictCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GhnService> _logger;

    public GhnService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GhnService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured()
    {
        var hasApiUrl = !string.IsNullOrWhiteSpace(_configuration["Ghn:ApiUrl"]);
        var hasToken = !string.IsNullOrWhiteSpace(_configuration["Ghn:Token"]);
        var hasShopId = int.TryParse(_configuration["Ghn:ShopId"], out var shopId) && shopId > 0;

        return hasApiUrl && hasToken && hasShopId;
    }

    public bool ValidateWebhookToken(string? tokenHeader)
    {
        var expected = _configuration["Ghn:WebhookToken"]?.Trim();
        if (string.IsNullOrWhiteSpace(expected))
        {
            expected = _configuration["Ghn:Token"]?.Trim();
        }

        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(tokenHeader)
            && string.Equals(expected, tokenHeader.Trim(), StringComparison.Ordinal);
    }

    public async Task<List<GhnProvinceLookupDto>> GetProvincesAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var provinces = await GetAsync<List<GhnProvinceData>>("master-data/province", cancellationToken);

        return provinces
            .Where(p => p.ProvinceID > 0)
            .Select(p => new GhnProvinceLookupDto
            {
                ProvinceId = p.ProvinceID,
                ProvinceName = p.ProvinceName?.Trim() ?? string.Empty,
                Code = string.IsNullOrWhiteSpace(p.Code) ? null : p.Code.Trim()
            })
            .OrderBy(p => p.ProvinceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<GhnDistrictLookupDto>> GetDistrictsAsync(int provinceId, CancellationToken cancellationToken = default)
    {
        if (provinceId <= 0)
        {
            throw new ArgumentException("provinceId must be greater than 0.", nameof(provinceId));
        }

        EnsureConfigured();

        var districts = await PostAsync<List<GhnDistrictData>>(
            "master-data/district",
            new Dictionary<string, object?>
            {
                ["province_id"] = provinceId
            },
            cancellationToken);

        return districts
            .Where(d => d.DistrictID > 0)
            .Select(d => new GhnDistrictLookupDto
            {
                DistrictId = d.DistrictID,
                ProvinceId = d.ProvinceID,
                DistrictName = d.DistrictName?.Trim() ?? string.Empty,
                Code = string.IsNullOrWhiteSpace(d.Code) ? null : d.Code.Trim()
            })
            .OrderBy(d => d.DistrictName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<GhnWardLookupDto>> GetWardsAsync(int districtId, CancellationToken cancellationToken = default)
    {
        if (districtId <= 0)
        {
            throw new ArgumentException("districtId must be greater than 0.", nameof(districtId));
        }

        EnsureConfigured();

        var wards = await PostAsync<List<GhnWardData>>(
            "master-data/ward",
            new Dictionary<string, object?>
            {
                ["district_id"] = districtId
            },
            cancellationToken);

        return wards
            .Where(w => !string.IsNullOrWhiteSpace(w.WardCode))
            .Select(w => new GhnWardLookupDto
            {
                WardCode = w.WardCode!.Trim(),
                DistrictId = w.DistrictID,
                WardName = w.WardName?.Trim() ?? string.Empty
            })
            .OrderBy(w => w.WardName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<int> ResolveDistrictIdByWardAsync(
        string wardCode,
        int? provinceId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(wardCode))
        {
            throw new ArgumentException("GHN ward code is required.", nameof(wardCode));
        }

        EnsureConfigured();

        var normalizedWardCode = wardCode.Trim();
        var cacheKey = provinceId.HasValue && provinceId.Value > 0
            ? $"{provinceId.Value}:{normalizedWardCode}"
            : $"*:{normalizedWardCode}";

        if (WardDistrictCache.TryGetValue(cacheKey, out var cachedDistrictId))
        {
            return cachedDistrictId;
        }

        IEnumerable<int> provinceIds;
        if (provinceId.HasValue && provinceId.Value > 0)
        {
            provinceIds = [provinceId.Value];
        }
        else
        {
            var provinces = await GetAsync<List<GhnProvinceData>>("master-data/province", cancellationToken);
            provinceIds = provinces.Select(p => p.ProvinceID).Distinct();
        }

        foreach (var currentProvinceId in provinceIds)
        {
            var districts = await PostAsync<List<GhnDistrictData>>(
                "master-data/district",
                new Dictionary<string, object?>
                {
                    ["province_id"] = currentProvinceId
                },
                cancellationToken);

            foreach (var district in districts)
            {
                var wards = await PostAsync<List<GhnWardData>>(
                    "master-data/ward",
                    new Dictionary<string, object?>
                    {
                        ["district_id"] = district.DistrictID
                    },
                    cancellationToken);

                if (wards.Any(w => string.Equals(w.WardCode, normalizedWardCode, StringComparison.OrdinalIgnoreCase)))
                {
                    WardDistrictCache[cacheKey] = district.DistrictID;
                    WardDistrictCache[$"*:{normalizedWardCode}"] = district.DistrictID;
                    return district.DistrictID;
                }
            }
        }

        throw new InvalidOperationException(
            $"Cannot resolve GHN district for ward code '{normalizedWardCode}'. Check ward code in GHN master-data.");
    }

    public async Task<decimal> CalculateShippingFeeAsync(GhnCalculateFeeRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ToWardCode))
        {
            throw new ArgumentException("GHN destination ward is required.", nameof(request.ToWardCode));
        }

        EnsureConfigured();

        var toDistrictId = request.ToDistrictId > 0
            ? request.ToDistrictId
            : await ResolveDistrictIdByWardAsync(
                request.ToWardCode,
                request.ProvinceId,
                cancellationToken);

        var fromDistrictId = await ResolveConfiguredFromDistrictIdAsync(cancellationToken);
        var serviceTypeId = request.ServiceTypeId ?? GetOptionalInt("Ghn:DefaultServiceTypeId") ?? 2;
        var payload = RemoveEmptyValues(new Dictionary<string, object?>
        {
            ["shop_id"] = GetRequiredInt("Ghn:ShopId"),
            ["from_district_id"] = fromDistrictId,
            ["service_type_id"] = serviceTypeId,
            ["to_district_id"] = toDistrictId,
            ["to_ward_code"] = request.ToWardCode.Trim(),
            ["insurance_value"] = ToGhnMoney(request.InsuranceValue),
            ["weight"] = request.Weight > 0 ? request.Weight : GetOptionalInt("Ghn:DefaultWeight") ?? 1000,
            ["length"] = request.Length > 0 ? request.Length : GetOptionalInt("Ghn:DefaultLength") ?? 20,
            ["width"] = request.Width > 0 ? request.Width : GetOptionalInt("Ghn:DefaultWidth") ?? 20,
            ["height"] = request.Height > 0 ? request.Height : GetOptionalInt("Ghn:DefaultHeight") ?? 10
        });

        var feeData = await PostAsync<GhnFeeData>("v2/shipping-order/fee", payload, cancellationToken);
        var fee = feeData.Total ?? feeData.TotalFee ?? feeData.ServiceFee;

        if (!fee.HasValue)
        {
            throw new InvalidOperationException("GHN did not return shipping fee.");
        }

        return fee.Value;
    }

    public async Task<GhnCreateOrderResponse> CreateShippingOrderAsync(GhnCreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OrderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId is required.", nameof(request.OrderId));
        }

        if (string.IsNullOrWhiteSpace(request.ToName)
            || string.IsNullOrWhiteSpace(request.ToPhone)
            || string.IsNullOrWhiteSpace(request.ToAddress)
            || request.ToDistrictId <= 0
            || string.IsNullOrWhiteSpace(request.ToWardCode))
        {
            throw new ArgumentException("Incomplete destination information for GHN order creation.");
        }

        EnsureConfigured();
        var fromDistrictId = await ResolveConfiguredFromDistrictIdAsync(cancellationToken);

        var payload = RemoveEmptyValues(new Dictionary<string, object?>
        {
            ["shop_id"] = GetRequiredInt("Ghn:ShopId"),
            ["payment_type_id"] = 1,
            ["required_note"] = _configuration["Ghn:RequiredNote"]?.Trim() ?? "KHONGCHOXEMHANG",
            ["note"] = $"Order #{request.OrderNumber}",
            ["from_name"] = _configuration["Ghn:SenderName"]?.Trim(),
            ["from_phone"] = _configuration["Ghn:SenderPhone"]?.Trim(),
            ["from_address"] = _configuration["Ghn:SenderAddress"]?.Trim(),
            ["from_ward_code"] = _configuration["Ghn:FromWardCode"]?.Trim(),
            ["from_district_id"] = fromDistrictId,
            ["to_name"] = request.ToName.Trim(),
            ["to_phone"] = request.ToPhone.Trim(),
            ["to_address"] = request.ToAddress.Trim(),
            ["to_ward_code"] = request.ToWardCode.Trim(),
            ["to_district_id"] = request.ToDistrictId,
            ["cod_amount"] = ToGhnMoney(request.CodAmount),
            ["insurance_value"] = ToGhnMoney(request.InsuranceValue),
            ["content"] = string.IsNullOrWhiteSpace(request.Content) ? $"Order #{request.OrderNumber}" : request.Content.Trim(),
            ["weight"] = request.Weight > 0 ? request.Weight : GetOptionalInt("Ghn:DefaultWeight") ?? 1000,
            ["length"] = request.Length > 0 ? request.Length : GetOptionalInt("Ghn:DefaultLength") ?? 20,
            ["width"] = request.Width > 0 ? request.Width : GetOptionalInt("Ghn:DefaultWidth") ?? 20,
            ["height"] = request.Height > 0 ? request.Height : GetOptionalInt("Ghn:DefaultHeight") ?? 10,
            ["service_type_id"] = request.ServiceTypeId ?? GetOptionalInt("Ghn:DefaultServiceTypeId") ?? 2,
            ["client_order_code"] = request.OrderNumber,
            ["items"] = request.Items.Select(i => new Dictionary<string, object?>
            {
                ["name"] = string.IsNullOrWhiteSpace(i.Name) ? "San pham" : i.Name.Trim(),
                ["code"] = Guid.NewGuid().ToString("N")[..12],
                ["quantity"] = Math.Max(1, i.Quantity),
                ["price"] = ToGhnMoney(i.Price),
                ["length"] = request.Length > 0 ? request.Length : GetOptionalInt("Ghn:DefaultLength") ?? 20,
                ["width"] = request.Width > 0 ? request.Width : GetOptionalInt("Ghn:DefaultWidth") ?? 20,
                ["height"] = request.Height > 0 ? request.Height : GetOptionalInt("Ghn:DefaultHeight") ?? 10,
                ["weight"] = i.Weight > 0 ? i.Weight : GetOptionalInt("Ghn:DefaultItemWeight") ?? 200,
                ["category"] = new Dictionary<string, object?>
                {
                    ["level1"] = "Nong san"
                }
            }).ToList()
        });

        var createData = await PostAsync<GhnCreateOrderData>("v2/shipping-order/create", payload, cancellationToken);

        return new GhnCreateOrderResponse
        {
            OrderCode = createData.OrderCode,
            ServiceId = createData.ServiceId,
            TotalFee = createData.TotalFee,
            RawStatus = createData.Status,
            ExpectedDeliveryTime = createData.ExpectedDeliveryTime
        };
    }

    public async Task<GhnShippingOrderDetailResponse> GetShippingOrderDetailAsync(
        string orderCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
        {
            throw new ArgumentException("GHN order code is required.", nameof(orderCode));
        }

        EnsureConfigured();

        var detailData = await PostAsync<GhnShippingOrderDetailData>(
            "v2/shipping-order/detail",
            new Dictionary<string, object?>
            {
                ["order_code"] = orderCode.Trim()
            },
            cancellationToken);

        return new GhnShippingOrderDetailResponse
        {
            OrderCode = detailData.OrderCode,
            ClientOrderCode = detailData.ClientOrderCode,
            Status = detailData.Status,
            CodAmount = detailData.CodAmount,
            ServiceId = detailData.ServiceId ?? detailData.ServiceTypeId,
            UpdatedDate = detailData.UpdatedDate
        };
    }

    private static long ToGhnMoney(decimal value)
    {
        var normalized = decimal.Round(Math.Max(0m, value), 0, MidpointRounding.AwayFromZero);
        return Convert.ToInt64(normalized, CultureInfo.InvariantCulture);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured())
        {
            throw new InvalidOperationException(
                "GHN integration is not configured. Please set Ghn:ApiUrl, Ghn:Token and Ghn:ShopId.");
        }
    }

    private async Task<TData> PostAsync<TData>(
        string relativePath,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken)
    {
        var apiUrl = GetRequiredString("Ghn:ApiUrl").TrimEnd('/');
        var url = $"{apiUrl}/{relativePath.TrimStart('/')}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        request.Headers.TryAddWithoutValidation("Token", GetRequiredString("Ghn:Token"));
        request.Headers.TryAddWithoutValidation(
            "ShopId",
            GetRequiredInt("Ghn:ShopId").ToString(CultureInfo.InvariantCulture));

        var client = _httpClientFactory.CreateClient("GHN");
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        var envelope = JsonSerializer.Deserialize<GhnEnvelope<TData>>(content, JsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"GHN API error ({relativePath}) HTTP {(int)response.StatusCode}: {content}");
        }

        if (envelope == null)
        {
            throw new InvalidOperationException($"Cannot parse GHN response ({relativePath}).");
        }

        if (envelope.Code != 200)
        {
            throw new InvalidOperationException(
                $"GHN API rejected request ({relativePath}) code {envelope.Code}: {envelope.Message}");
        }

        if (envelope.Data == null)
        {
            throw new InvalidOperationException($"GHN response data is empty ({relativePath}).");
        }

        _logger.LogInformation("GHN API success for {Path}", relativePath);
        return envelope.Data;
    }

    private async Task<TData> GetAsync<TData>(string relativePath, CancellationToken cancellationToken)
    {
        var apiUrl = GetRequiredString("Ghn:ApiUrl").TrimEnd('/');
        var url = $"{apiUrl}/{relativePath.TrimStart('/')}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Token", GetRequiredString("Ghn:Token"));

        if (int.TryParse(_configuration["Ghn:ShopId"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var shopId)
            && shopId > 0)
        {
            request.Headers.TryAddWithoutValidation("ShopId", shopId.ToString(CultureInfo.InvariantCulture));
        }

        var client = _httpClientFactory.CreateClient("GHN");
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        var envelope = JsonSerializer.Deserialize<GhnEnvelope<TData>>(content, JsonOptions);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"GHN API error ({relativePath}) HTTP {(int)response.StatusCode}: {content}");
        }

        if (envelope == null)
        {
            throw new InvalidOperationException($"Cannot parse GHN response ({relativePath}).");
        }

        if (envelope.Code != 200)
        {
            throw new InvalidOperationException(
                $"GHN API rejected request ({relativePath}) code {envelope.Code}: {envelope.Message}");
        }

        if (envelope.Data == null)
        {
            throw new InvalidOperationException($"GHN response data is empty ({relativePath}).");
        }

        return envelope.Data;
    }

    private async Task<int> ResolveConfiguredFromDistrictIdAsync(CancellationToken cancellationToken)
    {
        var fromDistrictId = GetOptionalInt("Ghn:FromDistrictId");
        if (fromDistrictId.HasValue && fromDistrictId.Value > 0)
        {
            return fromDistrictId.Value;
        }

        var fromWardCode = _configuration["Ghn:FromWardCode"]?.Trim();
        if (string.IsNullOrWhiteSpace(fromWardCode))
        {
            throw new InvalidOperationException(
                "Missing Ghn:FromDistrictId and Ghn:FromWardCode. Set at least one sender location field.");
        }

        var fromProvinceId = GetOptionalInt("Ghn:FromProvinceId");
        var resolvedDistrictId = await ResolveDistrictIdByWardAsync(
            fromWardCode,
            fromProvinceId,
            cancellationToken);

        _logger.LogInformation(
            "Resolved GHN FromDistrictId={DistrictId} from FromWardCode={WardCode}",
            resolvedDistrictId,
            fromWardCode);

        return resolvedDistrictId;
    }

    private string GetRequiredString(string key)
    {
        var value = _configuration[key]?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing configuration value: {key}");
        }

        return value;
    }

    private int GetRequiredInt(string key)
    {
        var value = _configuration[key]?.Trim();
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            throw new InvalidOperationException($"Missing or invalid configuration value: {key}");
        }

        return parsed;
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

    private static Dictionary<string, object?> RemoveEmptyValues(Dictionary<string, object?> source)
    {
        return source
            .Where(kv => kv.Value switch
            {
                null => false,
                string s => !string.IsNullOrWhiteSpace(s),
                _ => true
            })
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private sealed class GhnEnvelope<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    private sealed class GhnFeeData
    {
        [JsonPropertyName("total")]
        public decimal? Total { get; set; }

        [JsonPropertyName("service_fee")]
        public decimal? ServiceFee { get; set; }

        [JsonPropertyName("total_fee")]
        public decimal? TotalFee { get; set; }
    }

    private sealed class GhnCreateOrderData
    {
        [JsonPropertyName("order_code")]
        public string? OrderCode { get; set; }

        [JsonPropertyName("service_id")]
        public int? ServiceId { get; set; }

        [JsonPropertyName("total_fee")]
        public decimal? TotalFee { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("expected_delivery_time")]
        public string? ExpectedDeliveryTime { get; set; }
    }

    private sealed class GhnShippingOrderDetailData
    {
        [JsonPropertyName("order_code")]
        public string? OrderCode { get; set; }

        [JsonPropertyName("client_order_code")]
        public string? ClientOrderCode { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("cod_amount")]
        public decimal? CodAmount { get; set; }

        [JsonPropertyName("service_id")]
        public int? ServiceId { get; set; }

        [JsonPropertyName("service_type_id")]
        public int? ServiceTypeId { get; set; }

        [JsonPropertyName("updated_date")]
        public DateTime? UpdatedDate { get; set; }
    }

    private sealed class GhnProvinceData
    {
        [JsonPropertyName("ProvinceID")]
        public int ProvinceID { get; set; }

        [JsonPropertyName("ProvinceName")]
        public string? ProvinceName { get; set; }

        [JsonPropertyName("Code")]
        public string? Code { get; set; }
    }

    private sealed class GhnDistrictData
    {
        [JsonPropertyName("DistrictID")]
        public int DistrictID { get; set; }

        [JsonPropertyName("ProvinceID")]
        public int ProvinceID { get; set; }

        [JsonPropertyName("DistrictName")]
        public string? DistrictName { get; set; }

        [JsonPropertyName("Code")]
        public string? Code { get; set; }
    }

    private sealed class GhnWardData
    {
        [JsonPropertyName("WardCode")]
        public string? WardCode { get; set; }

        [JsonPropertyName("DistrictID")]
        public int DistrictID { get; set; }

        [JsonPropertyName("WardName")]
        public string? WardName { get; set; }
    }
}
