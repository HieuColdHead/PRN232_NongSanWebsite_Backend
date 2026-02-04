using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IGoogleOAuthService
{
    GoogleOAuthStartResponse BuildAuthorizationUrl();
    Task<AuthResponse> ExchangeCodeAndLoginAsync(string code, string state);
}
