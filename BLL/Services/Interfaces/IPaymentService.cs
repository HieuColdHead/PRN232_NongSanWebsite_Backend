using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IPaymentService
{
    Task<PaymentDto?> GetByOrderIdAsync(Guid orderId);
    Task<PaymentDto> CreateAsync(CreatePaymentRequest request);
    Task<PaymentDto> UpdateStatusAsync(Guid paymentId, string status);
}
