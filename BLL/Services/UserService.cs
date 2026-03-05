using System.Linq;
using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace BLL.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly ILogger<UserService> _logger;
    private readonly IEmailOtpRepository _emailOtpRepository;
    private readonly IEmailSender _emailSender;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUserRepository userRepository, ITokenService tokenService, ILogger<UserService> logger, IEmailOtpRepository emailOtpRepository, IEmailSender emailSender, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _logger = logger;
        _emailOtpRepository = emailOtpRepository;
        _emailSender = emailSender;
        _passwordHasher = passwordHasher;
    }

    public async Task<PagedResult<UserDto>> GetUsersAsync(int pageNumber, int pageSize)
    {
        var (users, total) = await _userRepository.GetPagedAsync(pageNumber, pageSize);
        var items = users.Select(MapToDto);

        return new PagedResult<UserDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<UserDto?> GetByIdAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user == null ? null : MapToDto(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var user = new User
        {
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            DisplayName = request.DisplayName,
            Provider = request.Provider,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _userRepository.CreateAsync(user);
        _logger.LogInformation("User created: {UserId}", created.Id);
        return MapToDto(created);
    }

    public Task<bool> UpdateAsync(Guid id, UpdateUserRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return _userRepository.UpdateAsync(id, request.DisplayName, request.Email, request.PhoneNumber, request.IsActive);
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        return _userRepository.DeleteAsync(id);
    }

    public async Task<bool> ForgotPassword(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            return false;
        }

        var token = Guid.NewGuid().ToString();
        var otp = new EmailOtp
        {
            Email = email,
            OtpHash = _passwordHasher.Hash(token),
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Purpose = "PasswordReset",
            UserId = user.Id
        };

        await _emailOtpRepository.AddAsync(otp);
        await _emailOtpRepository.SaveChangesAsync();

        var resetLink = $"https://prn-222-fe-nongxanh.vercel.app/reset-password?email={email}&token={token}";
        await _emailSender.SendAsync(email, "Password Reset", $"Click here to reset your password: {resetLink}");

        return true;
    }

    public async Task<bool> ResetPassword(string email, string token, string newPassword)
    {
        var otp = await _emailOtpRepository.GetLatestValidAsync(email, DateTime.UtcNow);
        if (otp == null || otp.Purpose != "PasswordReset" || !_passwordHasher.Verify(token, otp.OtpHash))
        {
            return false;
        }

        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            return false;
        }

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        await _userRepository.UpdateAsync(user);

        otp.ConsumedAt = DateTime.UtcNow;
        await _emailOtpRepository.SaveChangesAsync();

        return true;
    }

    private UserDto MapToDto(User user)
    {
        return new UserDto
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
        };
    }
}
