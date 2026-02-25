using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;

namespace BLL.Services;

public sealed class LocalAuthService : ILocalAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public LocalAuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _userRepository.EmailExistsAsync(email))
        {
            throw new InvalidOperationException("Email already exists.");
        }

        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName,
            Provider = "Local",
            Role = UserRole.User,
            PasswordHash = _passwordHasher.Hash(request.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(user);

        return new AuthResponse
        {
            AccessToken = _tokenService.CreateAccessToken(user),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                DisplayName = user.DisplayName,
                Provider = user.Provider,
                CreatedAt = user.CreatedAt,
                IsActive = user.IsActive,
                Role = _tokenService.ResolveRoleName(user),
                LastLoginAt = user.LastLoginAt
            }
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(email);

        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash) || user.Provider != "Local")
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("User is inactive.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = _tokenService.CreateAccessToken(user),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                DisplayName = user.DisplayName,
                Provider = user.Provider,
                CreatedAt = user.CreatedAt,
                IsActive = user.IsActive,
                Role = _tokenService.ResolveRoleName(user),
                LastLoginAt = user.LastLoginAt
            }
        };
    }
}
