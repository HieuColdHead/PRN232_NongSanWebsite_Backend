using BLL.Services.Interfaces;
using FirebaseAdmin.Auth;

namespace BLL.Services;

public class FirebaseAuthService : IFirebaseAuthService
{
    public async Task<FirebaseToken> VerifyTokenAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new ArgumentException("Firebase ID token is required.", nameof(idToken));
        }

        try
        {
            return await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken);
        }
        catch (FirebaseAuthException ex)
        {
            throw new UnauthorizedAccessException("Firebase ID token is invalid.", ex);
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException("Unable to verify Firebase ID token.", ex);
        }
    }

    public async Task<UserRecord> CreateUserAsync(string email, string password, string? displayName, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }

        var args = new UserRecordArgs
        {
            Email = email,
            EmailVerified = false,
            Password = password,
            DisplayName = displayName,
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber,
            Disabled = false
        };

        try
        {
            return await FirebaseAuth.DefaultInstance.CreateUserAsync(args);
        }
        catch (FirebaseAuthException ex)
        {
            var reason = ex.AuthErrorCode.ToString();
            var message = $"Firebase create user failed: {reason}";
            throw new InvalidOperationException(message, ex);
        }
    }

    public async Task DeleteUserAsync(string firebaseUid)
    {
        if (string.IsNullOrWhiteSpace(firebaseUid))
        {
            return;
        }

        try
        {
            await FirebaseAuth.DefaultInstance.DeleteUserAsync(firebaseUid);
        }
        catch
        {
            // best-effort rollback; do not throw
        }
    }
}
