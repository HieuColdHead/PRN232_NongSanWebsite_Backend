using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;

namespace BLL.Services;

public class PaymentService : IPaymentService
{
    private readonly IGenericRepository<Payment> _repository;

    public PaymentService(IGenericRepository<Payment> repository)
    {
        _repository = repository;
    }

    public async Task<PaymentDto?> GetByOrderIdAsync(int orderId)
    {
        var payments = await _repository.FindAsync(p => p.OrderId == orderId);
        var payment = payments.FirstOrDefault();
        if (payment == null) return null;
        return MapToDto(payment);
    }

    public async Task<PaymentDto> CreateAsync(CreatePaymentRequest request)
    {
        var payment = new Payment
        {
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = "Pending",
            OrderId = request.OrderId
        };

        await _repository.AddAsync(payment);
        await _repository.SaveChangesAsync();

        return MapToDto(payment);
    }

    public async Task<PaymentDto> UpdateStatusAsync(int paymentId, string status)
    {
        var payment = await _repository.GetByIdAsync(paymentId)
            ?? throw new KeyNotFoundException($"Payment {paymentId} not found");

        payment.PaymentStatus = status;
        if (status == "Paid")
            payment.PaidAt = DateTime.UtcNow;

        await _repository.UpdateAsync(payment);
        await _repository.SaveChangesAsync();

        return MapToDto(payment);
    }

    private static PaymentDto MapToDto(Payment payment)
    {
        return new PaymentDto
        {
            PaymentId = payment.PaymentId,
            PaymentMethod = payment.PaymentMethod,
            PaymentStatus = payment.PaymentStatus,
            PaidAt = payment.PaidAt,
            OrderId = payment.OrderId
        };
    }
}
