using DAL.Entity;

namespace BLL.Services.Interfaces;

public interface ITokenService
{
    string CreateAccessToken(User user);
    
    /// <summary>
    /// Resolves the role name for a given user (e.g. "Admin", "User").
    /// </summary>
    string ResolveRoleName(User user);
}
