using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
[Authorize]
public class VouchersController : BaseApiController
{
    private readonly IVoucherService _service;

    public VouchersController(IVoucherService service)
    {
        _service = service;
    }

    /// <summary>
    /// Any user (including anonymous) can view vouchers.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<VoucherDto>>>> GetVouchers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        if (pageNumber < 1 || pageSize < 1)
        {
            return ErrorResponse<PagedResult<VoucherDto>>("Page number and page size must be greater than 0.");
        }

        var result = await _service.GetPagedAsync(pageNumber, pageSize);
        return SuccessResponse(result);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<VoucherDto>>> GetVoucher(int id)
    {
        var voucher = await _service.GetByIdAsync(id);

        if (voucher == null)
        {
            return ErrorResponse<VoucherDto>("Voucher not found", statusCode: 404);
        }

        return SuccessResponse(voucher);
    }

    [HttpGet("code/{code}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<VoucherDto>>> GetByCode(string code)
    {
        var voucher = await _service.GetByCodeAsync(code);

        if (voucher == null)
        {
            return ErrorResponse<VoucherDto>("Voucher not found", statusCode: 404);
        }

        return SuccessResponse(voucher);
    }

    /// <summary>
    /// Admin only: create a voucher.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<VoucherDto>>> PostVoucher(CreateVoucherRequest request)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<VoucherDto>("Forbidden", statusCode: 403);
        }

        var voucher = await _service.CreateAsync(request);
        return SuccessResponse(voucher, "Voucher created successfully");
    }

    /// <summary>
    /// Admin only: update a voucher.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> PutVoucher(int id, UpdateVoucherRequest request)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        if (id != request.VoucherId)
        {
            return ErrorResponse<object>("Voucher ID mismatch");
        }

        await _service.UpdateAsync(request);
        return SuccessResponse("Voucher updated successfully");
    }

    /// <summary>
    /// Admin only: delete a voucher.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteVoucher(int id)
    {
        if (!IsAdmin())
        {
            return ErrorResponse<object>("Forbidden", statusCode: 403);
        }

        await _service.DeleteAsync(id);
        return SuccessResponse("Voucher deleted successfully");
    }
}
