using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public sealed class GoogleOAuthCallbackRequest
{
    [Required]
    public string Code { get; init; } = string.Empty;

    [Required]
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// Optional redirect URI used by mobile app (app scheme).
    /// If provided, this will be used instead of the server-configured redirect URI.
    /// </summary>
    public string? RedirectUri { get; init; }
}
