using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public sealed class GoogleOAuthCallbackRequest
{
    [Required]
    public string Code { get; init; } = string.Empty;

    [Required]
    public string State { get; init; } = string.Empty;
}
