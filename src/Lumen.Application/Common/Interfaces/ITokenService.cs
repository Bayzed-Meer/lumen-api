namespace Lumen.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(string userId, string email, string role);
    string GenerateRefreshToken();
}
