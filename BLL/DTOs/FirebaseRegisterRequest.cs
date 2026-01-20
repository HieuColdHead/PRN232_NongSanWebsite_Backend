namespace BLL.DTOs;

public class FirebaseRegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
}
