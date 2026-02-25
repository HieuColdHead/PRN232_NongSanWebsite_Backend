using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IVoucherService
{
    Task<PagedResult<VoucherDto>> GetPagedAsync(int pageNumber, int pageSize);
    Task<VoucherDto?> GetByIdAsync(int id);
    Task<VoucherDto?> GetByCodeAsync(string code);
    Task<VoucherDto> CreateAsync(CreateVoucherRequest request);
    Task UpdateAsync(UpdateVoucherRequest request);
    Task DeleteAsync(int id);
}
