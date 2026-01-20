namespace BLL.DTOs;

public class AuthResponse
{
    public Guid UserId { get; set; }
    public string FirebaseUid { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? DisplayName { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
}
