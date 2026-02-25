using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IPaymentService
{
    Task<PaymentDto?> GetByOrderIdAsync(int orderId);
    Task<PaymentDto> CreateAsync(CreatePaymentRequest request);
    Task<PaymentDto> UpdateStatusAsync(int paymentId, string status);
}
