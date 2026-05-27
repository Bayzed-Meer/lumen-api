using Lumen.Application.Common.Exceptions;
using Lumen.Application.Common.Helpers;
using Lumen.Application.Common.Interfaces;
using Lumen.Application.Common.Settings;
using Lumen.Application.DTOs.Auth;
using Lumen.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Lumen.Application.Services.Auth;

public sealed class AuthService(
    IIdentityService identityService,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepository,
    IOptions<JwtSettings> jwtSettings) : IAuthService
{
    public async Task<(LoginResponse Response, string RawRefreshToken)> LoginAsync(
        LoginRequest request,
        CancellationToken ct = default)
    {
        string? userId = await identityService.ResolveUserIdAsync(request.Identity, ct);

        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedException("Invalid credentials.");

        bool isVerified = await identityService.IsVerifiedAsync(userId, ct);
        if (!isVerified)
            throw new UnauthorizedException("Invalid credentials.");

        bool passwordValid = await identityService.CheckPasswordAsync(userId, request.Password, ct);
        if (!passwordValid)
            throw new UnauthorizedException("Invalid credentials.");

        UserRole role = await identityService.GetUserRoleAsync(userId, ct);
        string roleName = role.ToString();
        string email = await identityService.GetUserEmailAsync(userId, ct);

        string accessToken = tokenService.GenerateAccessToken(userId, email, roleName);
        string rawRefreshToken = tokenService.GenerateRefreshToken();
        string tokenHash = AuthCrypto.HashSecret(rawRefreshToken);

        await refreshTokenRepository.AddAsync(
            tokenHash,
            userId,
            DateTimeOffset.UtcNow.AddDays(jwtSettings.Value.RefreshTokenExpiryDays),
            ct: ct);

        LoginResponse response = new() { AccessToken = accessToken, ExpiresInSeconds = jwtSettings.Value.AccessTokenExpiryMinutes * 60 };
        return (response, rawRefreshToken);
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        string hash = AuthCrypto.HashSecret(rawRefreshToken);
        await refreshTokenRepository.RevokeAsync(hash, ct);
    }

    public async Task<(LoginResponse Response, string NewRawRefreshToken)> RefreshAsync(
        string rawRefreshToken,
        CancellationToken ct = default)
    {
        string hash = AuthCrypto.HashSecret(rawRefreshToken);
        RefreshTokenInfo tokenInfo = await refreshTokenRepository.GetByTokenHashAsync(hash, ct)
            ?? throw new UnauthorizedException("Invalid refresh token.");

        if (tokenInfo.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new UnauthorizedException("Refresh token has expired.");

        if (tokenInfo.IsRevoked)
        {
            await refreshTokenRepository.RevokeAllForUserAsync(tokenInfo.UserId, ct);
            throw new UnauthorizedException("Refresh token reuse detected. All sessions revoked.");
        }

        await refreshTokenRepository.RevokeAsync(hash, ct);

        UserRole role;
        string email;
        try
        {
            role = await identityService.GetUserRoleAsync(tokenInfo.UserId, ct);
            email = await identityService.GetUserEmailAsync(tokenInfo.UserId, ct);
        }
        catch (NotFoundException)
        {
            throw new UnauthorizedException("Invalid session.");
        }

        string roleName = role.ToString();

        string newRawRefreshToken = tokenService.GenerateRefreshToken();
        string newTokenHash = AuthCrypto.HashSecret(newRawRefreshToken);

        await refreshTokenRepository.AddAsync(
            newTokenHash,
            tokenInfo.UserId,
            DateTimeOffset.UtcNow.AddDays(jwtSettings.Value.RefreshTokenExpiryDays),
            ct);
        string accessToken = tokenService.GenerateAccessToken(tokenInfo.UserId, email, roleName);
        LoginResponse response = new() { AccessToken = accessToken, ExpiresInSeconds = jwtSettings.Value.AccessTokenExpiryMinutes * 60 };
        return (response, newRawRefreshToken);
    }
}
