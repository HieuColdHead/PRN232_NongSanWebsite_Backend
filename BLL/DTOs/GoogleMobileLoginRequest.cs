using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs;

public sealed class GoogleMobileLoginRequest
{
    [Required]
    public string IdToken { get; init; } = string.Empty;
}

