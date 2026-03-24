using BLL.DTOs.Ghn;

namespace BLL.Services.Interfaces;

public interface IGhnService
{
    bool IsConfigured();
    bool ValidateWebhookToken(string? tokenHeader);
    Task<List<GhnProvinceLookupDto>> GetProvincesAsync(CancellationToken cancellationToken = default);
    Task<List<GhnDistrictLookupDto>> GetDistrictsAsync(int provinceId, CancellationToken cancellationToken = default);
    Task<List<GhnWardLookupDto>> GetWardsAsync(int districtId, CancellationToken cancellationToken = default);
    Task<List<GhnWardLookupDto>> GetWardsByProvinceAsync(int provinceId, CancellationToken cancellationToken = default);
    Task<int> ResolveDistrictIdByWardAsync(string wardCode, int? provinceId = null, CancellationToken cancellationToken = default);
    Task<decimal> CalculateShippingFeeAsync(GhnCalculateFeeRequest request, CancellationToken cancellationToken = default);
    Task<GhnCreateOrderResponse> CreateShippingOrderAsync(GhnCreateOrderRequest request, CancellationToken cancellationToken = default);
    Task<GhnShippingOrderDetailResponse> GetShippingOrderDetailAsync(string orderCode, CancellationToken cancellationToken = default);
}
