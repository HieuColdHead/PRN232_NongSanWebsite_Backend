using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IVoucherService
{
    Task<PagedResult<VoucherDto>> GetPagedAsync(int pageNumber, int pageSize);
    Task<VoucherDto?> GetByIdAsync(Guid id);
    Task<VoucherDto?> GetByCodeAsync(string code);
    Task<VoucherDto> CreateAsync(CreateVoucherRequest request);
    Task UpdateAsync(Guid id, UpdateVoucherRequest request);
    Task DeleteAsync(Guid id);
}
