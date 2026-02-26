using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;

namespace BLL.Services;

public class VoucherService : IVoucherService
{
    private readonly IGenericRepository<Voucher> _repository;

    public VoucherService(IGenericRepository<Voucher> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<VoucherDto>> GetPagedAsync(int pageNumber, int pageSize)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize);

        return new PagedResult<VoucherDto>
        {
            Items = items.Select(MapToDto),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<VoucherDto?> GetByIdAsync(int id)
    {
        var voucher = await _repository.GetByIdAsync(id);
        if (voucher == null) return null;
        return MapToDto(voucher);
    }

    public async Task<VoucherDto?> GetByCodeAsync(string code)
    {
        var vouchers = await _repository.FindAsync(v => v.Code == code);
        var voucher = vouchers.FirstOrDefault();
        if (voucher == null) return null;
        return MapToDto(voucher);
    }

    public async Task<VoucherDto> CreateAsync(CreateVoucherRequest request)
    {
        var voucher = new Voucher
        {
            Code = request.Code,
            Description = request.Description,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            MinOrderValue = request.MinOrderValue,
            MaxDiscount = request.MaxDiscount,
            Quantity = request.Quantity,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = "Active"
        };

        await _repository.AddAsync(voucher);
        await _repository.SaveChangesAsync();

        return MapToDto(voucher);
    }

    public async Task UpdateAsync(int id, UpdateVoucherRequest request)
    {
        var voucher = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Voucher {id} not found");

        if (request.Description != null) voucher.Description = request.Description;
        if (request.DiscountType != null) voucher.DiscountType = request.DiscountType;
        voucher.DiscountValue = request.DiscountValue;
        voucher.MinOrderValue = request.MinOrderValue;
        voucher.MaxDiscount = request.MaxDiscount;
        voucher.Quantity = request.Quantity;
        if (request.StartDate.HasValue) voucher.StartDate = request.StartDate;
        if (request.EndDate.HasValue) voucher.EndDate = request.EndDate;
        if (request.Status != null) voucher.Status = request.Status;

        await _repository.UpdateAsync(voucher);
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
    }

    private static VoucherDto MapToDto(Voucher voucher)
    {
        return new VoucherDto
        {
            VoucherId = voucher.VoucherId,
            Code = voucher.Code,
            Description = voucher.Description,
            DiscountType = voucher.DiscountType,
            DiscountValue = voucher.DiscountValue,
            MinOrderValue = voucher.MinOrderValue,
            MaxDiscount = voucher.MaxDiscount,
            Quantity = voucher.Quantity,
            StartDate = voucher.StartDate,
            EndDate = voucher.EndDate,
            Status = voucher.Status
        };
    }
}
