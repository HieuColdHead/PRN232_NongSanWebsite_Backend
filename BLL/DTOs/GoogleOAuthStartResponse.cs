namespace BLL.DTOs;

public sealed class GoogleOAuthStartResponse
{
    public required string AuthorizationUrl { get; init; }
    public required string State { get; init; }
}
