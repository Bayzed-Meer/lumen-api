using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Lumen.Application.Common.Constants;
using Lumen.Application.Common.Interfaces;
using Lumen.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Lumen.Infrastructure.Services;

public sealed class TokenService(IOptions<JwtOptions> jwtOptions) : ITokenService
{
    public string GenerateAccessToken(string userId, string email, string role)
    {
        JwtOptions options = jwtOptions.Value;
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(options.Key));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimNames.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];

        JwtSecurityToken token = new(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTimeOffset.UtcNow.AddMinutes(options.AccessTokenExpiryMinutes).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        byte[] bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
