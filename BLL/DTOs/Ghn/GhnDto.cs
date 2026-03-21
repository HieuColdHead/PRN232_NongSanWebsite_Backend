using System.Text.Json.Serialization;

namespace BLL.DTOs.Ghn;

public class GhnProvinceLookupDto
{
    public int ProvinceId { get; set; }
    public string ProvinceName { get; set; } = string.Empty;
    public string? Code { get; set; }
}

public class GhnDistrictLookupDto
{
    public int DistrictId { get; set; }
    public int ProvinceId { get; set; }
    public string DistrictName { get; set; } = string.Empty;
    public string? Code { get; set; }
}

public class GhnWardLookupDto
{
    public string WardCode { get; set; } = string.Empty;
    public int DistrictId { get; set; }
    public string WardName { get; set; } = string.Empty;
}

public class GhnCalculateFeeRequest
{
    public int ToDistrictId { get; set; }
    public string ToWardCode { get; set; } = string.Empty;
    public int? ProvinceId { get; set; }
    public decimal InsuranceValue { get; set; }
    public int Weight { get; set; }
    public int Length { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int? ServiceTypeId { get; set; }
}

public class GhnCreateOrderRequest
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public string ToPhone { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public int ToDistrictId { get; set; }
    public string ToWardCode { get; set; } = string.Empty;
    public decimal InsuranceValue { get; set; }
    public decimal CodAmount { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Weight { get; set; }
    public int Length { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int? ServiceTypeId { get; set; }
    public List<GhnCreateOrderItemDto> Items { get; set; } = new();
}

public class GhnCreateOrderItemDto
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public int Weight { get; set; }
}

public class GhnCreateOrderResponse
{
    public string? OrderCode { get; set; }
    public int? ServiceId { get; set; }
    public decimal? TotalFee { get; set; }
    public string? RawStatus { get; set; }
    public string? ExpectedDeliveryTime { get; set; }
}

public class GhnShippingOrderDetailResponse
{
    public string? OrderCode { get; set; }
    public string? ClientOrderCode { get; set; }
    public string? Status { get; set; }
    public decimal? CodAmount { get; set; }
    public int? ServiceId { get; set; }

    [JsonPropertyName("updated_date")]
    public DateTime? UpdatedDate { get; set; }
}

public class GhnWebhookRequest
{
    public string? OrderCode { get; set; }
    public string? ClientOrderCode { get; set; }
    public string? Status { get; set; }
    public string? Description { get; set; }
    public decimal? CodAmount { get; set; }

    [JsonPropertyName("updated_date")]
    public DateTime? UpdatedDate { get; set; }
}
