using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginWithFirebaseAsync(string idToken);
    Task<AuthResponse> RegisterWithEmailPasswordAsync(FirebaseRegisterRequest request);
}
