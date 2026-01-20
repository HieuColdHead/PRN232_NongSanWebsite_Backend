using DAL.Entity;

namespace BLL.Services.Interfaces;

public interface ITokenService
{
    string? GenerateToken(User user);
}
