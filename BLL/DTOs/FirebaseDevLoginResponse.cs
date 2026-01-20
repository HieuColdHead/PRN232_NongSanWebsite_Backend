namespace BLL.DTOs;

// DEV ONLY - DO NOT USE IN PRODUCTION
public class FirebaseDevLoginResponse
{
    public string IdToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

