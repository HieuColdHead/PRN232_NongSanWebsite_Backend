using BLL.DTOs.Ghn;

namespace BLL.Services.Interfaces;

public interface IGhnService
{
    bool IsConfigured();
    Task<List<GhnProvinceDto>> GetProvincesAsync(CancellationToken cancellationToken = default);
    Task<List<GhnDistrictDto>> GetDistrictsAsync(int provinceId, CancellationToken cancellationToken = default);
    Task<List<GhnWardDto>> GetWardsAsync(int districtId, CancellationToken cancellationToken = default);
    bool ValidateWebhookToken(string? tokenHeader);
    Task<int> ResolveDistrictIdByWardAsync(string wardCode, int? provinceId = null, CancellationToken cancellationToken = default);
    Task<decimal> CalculateShippingFeeAsync(GhnCalculateFeeRequest request, CancellationToken cancellationToken = default);
    Task<GhnCreateOrderResponse> CreateShippingOrderAsync(GhnCreateOrderRequest request, CancellationToken cancellationToken = default);
    Task<GhnShippingOrderDetailResponse> GetShippingOrderDetailAsync(string orderCode, CancellationToken cancellationToken = default);
}
