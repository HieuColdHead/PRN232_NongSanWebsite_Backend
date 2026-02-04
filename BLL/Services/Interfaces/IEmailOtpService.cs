using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IEmailOtpService
{
    Task RequestOtpAsync(string email);
    Task<AuthResponse> VerifyOtpAndRegisterAsync(EmailOtpVerifyRequest request);
}
