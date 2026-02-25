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

    public UserService(IUserRepository userRepository, ITokenService tokenService, ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _logger = logger;
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
