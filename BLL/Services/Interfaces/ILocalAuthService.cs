using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface ILocalAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
}
