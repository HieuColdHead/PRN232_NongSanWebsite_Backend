namespace BLL.DTOs;

public sealed class AuthResponse
{
    public required string AccessToken { get; init; }
    public required UserDto User { get; init; }
}
