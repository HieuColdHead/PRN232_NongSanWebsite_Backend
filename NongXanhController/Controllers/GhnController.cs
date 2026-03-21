using BLL.DTOs;
using BLL.DTOs.Ghn;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GhnController : BaseApiController
{
    private readonly IGhnService _ghnService;

    public GhnController(IGhnService ghnService)
    {
        _ghnService = ghnService;
    }

    /// <summary>Get all provinces for address picker. AllowAnonymous for form use.</summary>
    [HttpGet("provinces")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<GhnProvinceLookupDto>>>> GetProvinces(CancellationToken ct)
    {
        if (!_ghnService.IsConfigured())
            return ErrorResponse<List<GhnProvinceLookupDto>>("GHN chưa được cấu hình.", statusCode: 503);

        var list = await _ghnService.GetProvincesAsync(ct);
        return SuccessResponse(list);
    }

    /// <summary>Get districts by province ID.</summary>
    [HttpGet("districts")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<GhnDistrictLookupDto>>>> GetDistricts(
        [FromQuery] int provinceId,
        CancellationToken ct)
    {
        if (!_ghnService.IsConfigured())
            return ErrorResponse<List<GhnDistrictLookupDto>>("GHN chưa được cấu hình.", statusCode: 503);
        if (provinceId <= 0)
            return ErrorResponse<List<GhnDistrictLookupDto>>("provinceId phải lớn hơn 0.");

        var list = await _ghnService.GetDistrictsAsync(provinceId, ct);
        return SuccessResponse(list);
    }

    /// <summary>Get wards by district ID.</summary>
    [HttpGet("wards")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<GhnWardLookupDto>>>> GetWards(
        [FromQuery] int districtId,
        CancellationToken ct)
    {
        if (!_ghnService.IsConfigured())
            return ErrorResponse<List<GhnWardLookupDto>>("GHN chưa được cấu hình.", statusCode: 503);
        if (districtId <= 0)
            return ErrorResponse<List<GhnWardLookupDto>>("districtId phải lớn hơn 0.");

        var list = await _ghnService.GetWardsAsync(districtId, ct);
        return SuccessResponse(list);
    }
}
