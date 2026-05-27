using Lumen.Application.Common.Exceptions;
using Lumen.Application.Common.Interfaces;
using Lumen.Application.Common.Settings;
using Lumen.Application.DTOs.Auth;
using Lumen.Application.Services.Auth;
using Lumen.Domain.Enums;
using Microsoft.Extensions.Options;
using Moq;

namespace Lumen.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IIdentityService> _identityService = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepository = new();
    private readonly IAuthService _sut;

    public AuthServiceTests()
    {
        IOptions<JwtSettings> jwtSettings = Options.Create(new JwtSettings
        {
            AccessTokenExpiryMinutes = 60,
            RefreshTokenExpiryDays = 7
        });

        _sut = new AuthService(_identityService.Object, _tokenService.Object, _refreshTokenRepository.Object, jwtSettings);
    }

    // ── LoginAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidEmailAndPassword_ReturnsLoginResponseWithRefreshToken()
    {
        _identityService.Setup(x => x.ResolveUserIdAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-1");
        _identityService.Setup(x => x.CheckPasswordAsync("uid-1", "P@ssword1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _identityService.Setup(x => x.IsVerifiedAsync("uid-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _identityService.Setup(x => x.GetUserRoleAsync("uid-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Student);
        _identityService.Setup(x => x.GetUserEmailAsync("uid-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("user@uni.edu");
        _tokenService.Setup(x => x.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("access-token");
        _tokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("raw-refresh-token");

        var (response, rawRefreshToken) = await _sut.LoginAsync(new LoginRequest { Identity = "user@uni.edu", Password = "P@ssword1" });

        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal("raw-refresh-token", rawRefreshToken);
        _refreshTokenRepository.Verify(x => x.AddAsync(
            It.IsAny<string>(), "uid-1", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ValidInstitutionalIdAndPassword_ReturnsLoginResponse()
    {
        _identityService.Setup(x => x.ResolveUserIdAsync("STU-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-2");
        _identityService.Setup(x => x.CheckPasswordAsync("uid-2", "P@ssword1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _identityService.Setup(x => x.IsVerifiedAsync("uid-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _identityService.Setup(x => x.GetUserRoleAsync("uid-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Student);
        _identityService.Setup(x => x.GetUserEmailAsync("uid-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync("student@uni.edu");
        _tokenService.Setup(x => x.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("access-token");
        _tokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("raw-refresh");

        var (response, _) = await _sut.LoginAsync(new LoginRequest { Identity = "STU-001", Password = "P@ssword1" });

        Assert.Equal("access-token", response.AccessToken);
    }

    [Fact]
    public async Task LoginAsync_UnverifiedAccount_ThrowsUnauthorizedException()
    {
        _identityService.Setup(x => x.ResolveUserIdAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-3");
        _identityService.Setup(x => x.CheckPasswordAsync("uid-3", "P@ssword1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _identityService.Setup(x => x.IsVerifiedAsync("uid-3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.LoginAsync(new LoginRequest { Identity = "user@uni.edu", Password = "P@ssword1" }));
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorizedException()
    {
        _identityService.Setup(x => x.ResolveUserIdAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-4");
        _identityService.Setup(x => x.CheckPasswordAsync("uid-4", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.LoginAsync(new LoginRequest { Identity = "user@uni.edu", Password = "wrong" }));
    }

    [Fact]
    public async Task LoginAsync_NonExistentUser_ThrowsUnauthorizedException()
    {
        _identityService.Setup(x => x.ResolveUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.LoginAsync(new LoginRequest { Identity = "ghost@uni.edu", Password = "P@ssword1" }));
    }

    // ── LogoutAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task LogoutAsync_ValidToken_RevokesToken()
    {
        await _sut.LogoutAsync("raw-token");

        _refreshTokenRepository.Verify(
            x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_UnknownToken_CompletesWithoutError()
    {
        _refreshTokenRepository.Setup(x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.LogoutAsync("unknown-token");
    }

    // ── RefreshAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_ValidToken_ReturnsNewPairAndRevokesOld()
    {
        var tokenInfo = new RefreshTokenInfo(Guid.NewGuid(), "uid-r1", IsRevoked: false, DateTimeOffset.UtcNow.AddDays(6));

        _refreshTokenRepository.Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenInfo);
        _identityService.Setup(x => x.GetUserRoleAsync("uid-r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Faculty);
        _identityService.Setup(x => x.GetUserEmailAsync("uid-r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("faculty@uni.edu");
        _tokenService.Setup(x => x.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("new-access-token");
        _tokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("new-raw-refresh");

        var (response, newRaw) = await _sut.RefreshAsync("old-raw-token");

        Assert.Equal("new-access-token", response.AccessToken);
        Assert.Equal("new-raw-refresh", newRaw);
        _refreshTokenRepository.Verify(x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenRepository.Verify(x => x.AddAsync(
            It.IsAny<string>(), "uid-r1", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_RevokedToken_ThrowsUnauthorizedAndRevokesFamily()
    {
        var tokenInfo = new RefreshTokenInfo(Guid.NewGuid(), "uid-r2", IsRevoked: true, DateTimeOffset.UtcNow.AddDays(6));

        _refreshTokenRepository.Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenInfo);

        await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.RefreshAsync("stolen-token"));

        _refreshTokenRepository.Verify(
            x => x.RevokeAllForUserAsync("uid-r2", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
