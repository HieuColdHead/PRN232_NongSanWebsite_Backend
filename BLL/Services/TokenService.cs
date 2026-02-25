using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BLL.Services.Interfaces;
using DAL.Entity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BLL.Services;

public sealed class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly HashSet<string> _adminEmails;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
        _adminEmails = configuration.GetSection("AdminEmails")
            .Get<string[]>()
            ?.Select(e => e.Trim().ToLowerInvariant())
            .ToHashSet() ?? new HashSet<string>();
    }

    public string CreateAccessToken(User user)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Missing Jwt:Key configuration.");
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        var expiresMinutes = int.TryParse(_configuration["Jwt:ExpiresMinutes"], out var m) ? m : 60;

        var role = ResolveRole(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("provider", user.Provider ?? string.Empty),
            new(ClaimTypes.Role, role.ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: string.IsNullOrWhiteSpace(issuer) ? null : issuer,
            audience: string.IsNullOrWhiteSpace(audience) ? null : audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string ResolveRoleName(User user) => ResolveRole(user).ToString();

    private UserRole ResolveRole(User user)
    {
        if (!string.IsNullOrWhiteSpace(user.Email)
            && _adminEmails.Contains(user.Email.Trim().ToLowerInvariant()))
        {
            return UserRole.Admin;
        }

        return user.Role;
    }
}
