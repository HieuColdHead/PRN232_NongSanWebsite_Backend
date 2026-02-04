using DAL.Entity;

namespace BLL.Services.Interfaces;

public interface ITokenService
{
    string CreateAccessToken(User user);
}
