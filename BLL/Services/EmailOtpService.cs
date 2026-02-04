using System.Security.Cryptography;
using System.Text;
using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Data;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BLL.Services;

public sealed class EmailOtpService : IEmailOtpService
{
    private readonly IEmailOtpRepository _otpRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly ITokenService _tokenService;
    private readonly ApplicationDbContext _dbContext;

    public EmailOtpService(
        IEmailOtpRepository otpRepository,
        IUserRepository userRepository,
        IEmailSender emailSender,
        ITokenService tokenService,
        ApplicationDbContext dbContext)
    {
        _otpRepository = otpRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _tokenService = tokenService;
        _dbContext = dbContext;
    }

    public async Task RequestOtpAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        // Generate a 6-digit OTP
        var otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var otpHash = HashOtp(email, otp);

        var entity = new EmailOtp
        {
            Email = email.Trim().ToLowerInvariant(),
            OtpHash = otpHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        await _otpRepository.AddAsync(entity);
        await _otpRepository.SaveChangesAsync();

        await _emailSender.SendAsync(
            email,
            "Your OTP code",
            $"Your OTP code is: {otp}. It expires in 10 minutes.");
    }

    public async Task<AuthResponse> VerifyOtpAndRegisterAsync(EmailOtpVerifyRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var otp = request.Otp.Trim();

        var latest = await _otpRepository.GetLatestValidAsync(email, DateTime.UtcNow);
        if (latest is null)
        {
            throw new UnauthorizedAccessException("OTP is invalid or expired.");
        }

        var expectedHash = HashOtp(email, otp);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(latest.OtpHash),
                Encoding.UTF8.GetBytes(expectedHash)))
        {
            throw new UnauthorizedAccessException("OTP is invalid or expired.");
        }

        latest.ConsumedAt = DateTime.UtcNow;

        // If user already exists -> just login
        var existing = await _userRepository.GetByEmailAsync(email);
        if (existing is not null)
        {
            existing.LastLoginAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(existing);
            await _userRepository.SaveChangesAsync();
            await _otpRepository.SaveChangesAsync();

            return new AuthResponse
            {
                AccessToken = _tokenService.CreateAccessToken(existing),
                User = new UserDto
                {
                    Id = existing.Id,
                    Email = existing.Email,
                    PhoneNumber = existing.PhoneNumber,
                    DisplayName = existing.DisplayName,
                    Provider = existing.Provider,
                    CreatedAt = existing.CreatedAt,
                    IsActive = existing.IsActive,
                    LastLoginAt = existing.LastLoginAt
                }
            };
        }

        // Finalize a staged (pending) local registration if available.
        var pending = await _dbContext.PendingRegistrations
            .FirstOrDefaultAsync(x => x.Email == email && x.ExpiresAt > DateTime.UtcNow);

        User user;
        if (pending is not null)
        {
            user = new User
            {
                Email = email,
                DisplayName = pending.DisplayName ?? request.DisplayName,
                PhoneNumber = pending.PhoneNumber ?? request.PhoneNumber,
                Provider = "Local",
                Role = UserRole.User,
                PasswordHash = pending.PasswordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            _dbContext.PendingRegistrations.Remove(pending);
        }
        else
        {
            // Backwards compatible: allow OTP-based email account without password (Provider="Email")
            user = new User
            {
                Email = email,
                DisplayName = request.DisplayName,
                PhoneNumber = request.PhoneNumber,
                Provider = "Email",
                Role = UserRole.User,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
        }

        await _userRepository.CreateAsync(user);
        await _otpRepository.SaveChangesAsync();
        await _dbContext.SaveChangesAsync();

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
                LastLoginAt = user.LastLoginAt
            }
        };
    }

    private static string HashOtp(string email, string otp)
    {
        // Simple deterministic hash: SHA256(email:otp)
        // Note: for higher security, add server-side secret (pepper) from config.
        var input = $"{email}:{otp}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
