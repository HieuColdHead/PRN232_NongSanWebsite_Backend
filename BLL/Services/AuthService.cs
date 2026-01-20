using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using FirebaseAdmin.Auth;
using Microsoft.Extensions.Logging;

namespace BLL.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IFirebaseAuthService _firebaseAuthService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IFirebaseAuthService firebaseAuthService,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _firebaseAuthService = firebaseAuthService;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthResponse> LoginWithFirebaseAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new ArgumentException("Firebase ID token is required.", nameof(idToken));
        }

        FirebaseToken firebaseToken = await _firebaseAuthService.VerifyTokenAsync(idToken);

        var uid = firebaseToken.Uid;
        var claims = firebaseToken.Claims;

        var email = ExtractClaimValue(claims, "email");
        var phoneNumber = ExtractClaimValue(claims, "phone_number");
        var displayName = ExtractClaimValue(claims, "name");
        var provider = ResolveSignInProvider(claims);

        var user = await _userRepository.GetByFirebaseUidAsync(uid);
        if (user == null)
        {
            user = new User
            {
                FirebaseUid = uid,
                Email = email,
                PhoneNumber = phoneNumber,
                DisplayName = displayName,
                Provider = provider,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

                await _userRepository.AddAsync(user);
        }
        else
        {
            bool isDirty = false;

            if (string.IsNullOrWhiteSpace(user.Email) && !string.IsNullOrWhiteSpace(email))
            {
                user.Email = email;
                isDirty = true;
            }

            if (string.IsNullOrWhiteSpace(user.PhoneNumber) && !string.IsNullOrWhiteSpace(phoneNumber))
            {
                user.PhoneNumber = phoneNumber;
                isDirty = true;
            }

            if (string.IsNullOrWhiteSpace(user.DisplayName) && !string.IsNullOrWhiteSpace(displayName))
            {
                user.DisplayName = displayName;
                isDirty = true;
            }

            if (string.IsNullOrWhiteSpace(user.Provider) && !string.IsNullOrWhiteSpace(provider))
            {
                user.Provider = provider;
                isDirty = true;
            }

            user.LastLoginAt = DateTime.UtcNow;

            if (isDirty)
            {
                await _userRepository.UpdateAsync(user);
            }
        }

        await _userRepository.SaveChangesAsync();

        var accessToken = _tokenService.GenerateToken(user);

        return new AuthResponse
        {
            UserId = user.Id,
            FirebaseUid = user.FirebaseUid,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            DisplayName = user.DisplayName,
            Provider = user.Provider,
            AccessToken = accessToken
        };
    }

    public async Task<AuthResponse> RegisterWithEmailPasswordAsync(FirebaseRegisterRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.", nameof(request.Email));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Password is required.", nameof(request.Password));
        }

        // Nếu local đã có email thì báo luôn (tránh tạo Firebase rồi fail)
        var localExists = await _userRepository.EmailExistsAsync(request.Email);
        if (localExists)
        {
            throw new InvalidOperationException("Email đã tồn tại trên hệ thống.");
        }

        // Tạo user trên Firebase
        var firebaseUser = await _firebaseAuthService.CreateUserAsync(
            request.Email,
            request.Password,
            request.DisplayName,
            request.PhoneNumber);

        var user = new User
        {
            FirebaseUid = firebaseUser.Uid,
            Email = firebaseUser.Email,
            PhoneNumber = firebaseUser.PhoneNumber,
            DisplayName = firebaseUser.DisplayName,
            Provider = "password",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        try
        {
            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Register succeeded on Firebase but failed on local DB. Rolling back Firebase user {FirebaseUid}", firebaseUser.Uid);
            await _firebaseAuthService.DeleteUserAsync(firebaseUser.Uid);
            throw;
        }

        return new AuthResponse
        {
            UserId = user.Id,
            FirebaseUid = user.FirebaseUid,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            DisplayName = user.DisplayName,
            Provider = user.Provider,
            AccessToken = _tokenService.GenerateToken(user)
        };
    }

    private static string? ExtractClaimValue(IReadOnlyDictionary<string, object> claims, string key)
    {
        return claims.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static string ResolveSignInProvider(IReadOnlyDictionary<string, object> claims)
    {
        if (claims.TryGetValue("firebase", out var firebaseObj) &&
            firebaseObj is IDictionary<string, object> firebaseDict &&
            firebaseDict.TryGetValue("sign_in_provider", out var providerObj))
        {
            return NormalizeProvider(providerObj?.ToString());
        }

        if (claims.TryGetValue("sign_in_provider", out var directProvider))
        {
            return NormalizeProvider(directProvider?.ToString());
        }

        return "unknown";
    }

    private static string NormalizeProvider(string? rawProvider)
    {
        if (string.IsNullOrWhiteSpace(rawProvider))
        {
            return "unknown";
        }

        return rawProvider switch
        {
            "google.com" => "google",
            "password" => "password",
            "phone" => "phone",
            _ => rawProvider
        };
    }
}
