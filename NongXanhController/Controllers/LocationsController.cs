using BLL.DTOs;
using BLL.DTOs.Ghn;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/locations")]
public class LocationsController : BaseApiController
{
    private readonly IGhnService _ghnService;

    public LocationsController(IGhnService ghnService)
    {
        _ghnService = ghnService;
    }

    [AllowAnonymous]
    [HttpGet("provinces")]
    public async Task<ActionResult<ApiResponse<List<GhnProvinceLookupDto>>>> GetProvinces(CancellationToken cancellationToken)
    {
        try
        {
            var provinces = await _ghnService.GetProvincesAsync(cancellationToken);
            return SuccessResponse(provinces);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse<List<GhnProvinceLookupDto>>(ex.Message, statusCode: 400);
        }
    }

    [AllowAnonymous]
    [HttpGet("districts")]
    public async Task<ActionResult<ApiResponse<List<GhnDistrictLookupDto>>>> GetDistricts(
        [FromQuery] int provinceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var districts = await _ghnService.GetDistrictsAsync(provinceId, cancellationToken);
            return SuccessResponse(districts);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<List<GhnDistrictLookupDto>>(ex.Message, statusCode: 400);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse<List<GhnDistrictLookupDto>>(ex.Message, statusCode: 400);
        }
    }

    [AllowAnonymous]
    [HttpGet("wards")]
    public async Task<ActionResult<ApiResponse<List<GhnWardLookupDto>>>> GetWards(
        [FromQuery] int districtId,
        CancellationToken cancellationToken)
    {
        try
        {
            var wards = await _ghnService.GetWardsAsync(districtId, cancellationToken);
            return SuccessResponse(wards);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<List<GhnWardLookupDto>>(ex.Message, statusCode: 400);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse<List<GhnWardLookupDto>>(ex.Message, statusCode: 400);
        }
    }
}
