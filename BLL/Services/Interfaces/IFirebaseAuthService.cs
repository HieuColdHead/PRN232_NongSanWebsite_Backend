using FirebaseAdmin.Auth;

namespace BLL.Services.Interfaces;

public interface IFirebaseAuthService
{
    Task<FirebaseToken> VerifyTokenAsync(string idToken);
    Task<UserRecord> CreateUserAsync(string email, string password, string? displayName, string? phoneNumber);
    Task DeleteUserAsync(string firebaseUid);
}
