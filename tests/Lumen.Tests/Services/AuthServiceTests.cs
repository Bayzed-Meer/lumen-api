using Lumen.Application.Common.Constants;
using Lumen.Application.Common.Exceptions;
using Lumen.Application.Common.Interfaces;
using Lumen.Application.Common.Settings;
using Lumen.Application.DTOs.Auth;
using Lumen.Application.Services.Auth;
using Lumen.Domain.Entities;
using Lumen.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Lumen.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IIdentityService> _identityService = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepository = new();
    private readonly Mock<IOtpRepository> _otpRepository = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<AuthService>> _logger = new();
    private readonly IAuthService _sut;

    public AuthServiceTests()
    {
        IOptions<JwtSettings> jwtSettings = Options.Create(new JwtSettings
        {
            AccessTokenExpiryMinutes = 60,
            RefreshTokenExpiryDays = 7
        });

        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> op, CancellationToken ct) => op(ct));
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task<(LoginResponse, string)>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<(LoginResponse, string)>> op, CancellationToken ct) => op(ct));
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task<string>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<string>> op, CancellationToken ct) => op(ct));

        _sut = new AuthService(
            _identityService.Object,
            _tokenService.Object,
            _refreshTokenRepository.Object,
            _otpRepository.Object,
            _emailService.Object,
            jwtSettings,
            _unitOfWork.Object,
            _logger.Object);

        _tokenService.Setup(x => x.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("access-token");
        _tokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("raw-refresh-token");
    }

    // ── LoginAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidEmailAndPassword_ReturnsLoginResponseWithRefreshToken()
    {
        _identityService.Setup(x => x.ResolveUserIdAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-1");
        _identityService.Setup(x => x.CheckPasswordAsync("uid-1", "test-only", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _identityService.Setup(x => x.IsVerifiedAsync("uid-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _identityService.Setup(x => x.GetUserRoleAsync("uid-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Student);
        _identityService.Setup(x => x.GetUserEmailAsync("uid-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("user@uni.edu");

        var (response, rawRefreshToken) = await _sut.LoginAsync(new LoginRequest { Identity = "user@uni.edu", Password = "test-only" });

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
        _identityService.Setup(x => x.CheckPasswordAsync("uid-2", "test-only", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _identityService.Setup(x => x.IsVerifiedAsync("uid-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _identityService.Setup(x => x.GetUserRoleAsync("uid-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Student);
        _identityService.Setup(x => x.GetUserEmailAsync("uid-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync("student@uni.edu");

        var (response, _) = await _sut.LoginAsync(new LoginRequest { Identity = "STU-001", Password = "test-only" });

        Assert.Equal("access-token", response.AccessToken);
    }

    [Fact]
    public async Task LoginAsync_UnverifiedAccount_ThrowsUnauthorizedException()
    {
        _identityService.Setup(x => x.ResolveUserIdAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-3");
        _identityService.Setup(x => x.CheckPasswordAsync("uid-3", "test-only", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _identityService.Setup(x => x.IsVerifiedAsync("uid-3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.LoginAsync(new LoginRequest { Identity = "user@uni.edu", Password = "test-only" }));
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
            () => _sut.LoginAsync(new LoginRequest { Identity = "ghost@uni.edu", Password = "test-only" }));
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

        var (response, newRaw) = await _sut.RefreshAsync("old-raw-token");

        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal("raw-refresh-token", newRaw);
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

    // ── ChangePasswordAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ChangePasswordAsync_Success_ReturnsNewTokensAndRevokesAllSessions()
    {
        _identityService.Setup(x => x.ChangePasswordAsync("uid-1", "OldPass1!", "NewPass2@", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _identityService.Setup(x => x.GetUserEmailAsync("uid-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("user@uni.edu");
        _identityService.Setup(x => x.GetUserRoleAsync("uid-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Student);

        var request = new ChangePasswordRequest { CurrentPassword = "OldPass1!", NewPassword = "NewPass2@" };
        var (response, rawRefreshToken) = await _sut.ChangePasswordAsync("uid-1", request);

        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal("raw-refresh-token", rawRefreshToken);
        _refreshTokenRepository.Verify(x => x.RevokeAllForUserAsync("uid-1", It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<string>(), "uid-1", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ThrowsValidationException()
    {
        _identityService.Setup(x => x.ChangePasswordAsync("uid-1", "WrongPass!", "NewPass2@", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new Dictionary<string, string[]>
            {
                ["currentPassword"] = ["Incorrect password."]
            }));

        var request = new ChangePasswordRequest { CurrentPassword = "WrongPass!", NewPassword = "NewPass2@" };

        await Assert.ThrowsAsync<ValidationException>(() => _sut.ChangePasswordAsync("uid-1", request));
    }

    [Fact]
    public async Task ChangePasswordAsync_ComplexityFailure_ThrowsValidationException()
    {
        _identityService.Setup(x => x.ChangePasswordAsync("uid-1", "OldPass1!", "weak", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new Dictionary<string, string[]>
            {
                ["newPassword"] = ["Password is too weak."]
            }));

        var request = new ChangePasswordRequest { CurrentPassword = "OldPass1!", NewPassword = "weak" };

        await Assert.ThrowsAsync<ValidationException>(() => _sut.ChangePasswordAsync("uid-1", request));
    }

    [Fact]
    public async Task ChangePasswordAsync_NewPasswordSameAsCurrent_ThrowsConflictException()
    {
        var request = new ChangePasswordRequest { CurrentPassword = "SamePass1!", NewPassword = "SamePass1!" };

        await Assert.ThrowsAsync<ConflictException>(() => _sut.ChangePasswordAsync("uid-1", request));
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_InvalidatesPasswordResetOtps()
    {
        _identityService.Setup(x => x.ChangePasswordAsync("uid-1", "OldPass1!", "NewPass2@", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _identityService.Setup(x => x.GetUserEmailAsync("uid-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("user@uni.edu");
        _identityService.Setup(x => x.GetUserRoleAsync("uid-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Student);

        var request = new ChangePasswordRequest { CurrentPassword = "OldPass1!", NewPassword = "NewPass2@" };
        await _sut.ChangePasswordAsync("uid-1", request);

        _otpRepository.Verify(x => x.InvalidateAllForUserAsync("uid-1", OtpPurpose.PasswordReset, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── ForgotPasswordAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPasswordAsync_RegisteredEmail_InvalidatesOldOtpAndSendsNew()
    {
        _identityService.Setup(x => x.FindUserByEmailAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-f1");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-f1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "user@uni.edu" });

        _otpRepository.Verify(x => x.InvalidateAllForUserAsync("uid-f1", OtpPurpose.PasswordReset, It.IsAny<CancellationToken>()), Times.Once);
        _otpRepository.Verify(x => x.AddAsync(It.Is<OtpRecord>(o => o.Purpose == OtpPurpose.PasswordReset), It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(x => x.SendPasswordResetOtpAsync("user@uni.edu", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_UnregisteredEmail_ReturnsWithNoSideEffects()
    {
        _identityService.Setup(x => x.FindUserByEmailAsync("unknown@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "unknown@uni.edu" });

        _otpRepository.Verify(x => x.AddAsync(It.IsAny<OtpRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(x => x.SendPasswordResetOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_UnverifiedUser_ReturnsWithNoSideEffects()
    {
        _identityService.Setup(x => x.FindUserByEmailAsync("unverified@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-unverified");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-unverified", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "unverified@uni.edu" });

        _otpRepository.Verify(x => x.AddAsync(It.IsAny<OtpRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(x => x.SendPasswordResetOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_EmailSendFailure_CompletesSuccessfully()
    {
        _identityService.Setup(x => x.FindUserByEmailAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-f2");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-f2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _emailService.Setup(x => x.SendPasswordResetOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP failure"));

        await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "user@uni.edu" });
    }

    [Fact]
    public async Task ForgotPasswordAsync_RegisteredEmail_LogsMessageContainingSent()
    {
        _identityService.Setup(x => x.FindUserByEmailAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-f3");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-f3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "user@uni.edu" });

        _logger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("sent")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ── ResendResetOtpAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ResendResetOtpAsync_RegisteredEmail_InvalidatesOldOtpAndSendsNew()
    {
        _identityService.Setup(x => x.FindUserByEmailAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-r1");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.ResendResetOtpAsync(new ResendResetOtpRequest { Email = "user@uni.edu" });

        _otpRepository.Verify(x => x.InvalidateAllForUserAsync("uid-r1", OtpPurpose.PasswordReset, It.IsAny<CancellationToken>()), Times.Once);
        _otpRepository.Verify(x => x.AddAsync(It.Is<OtpRecord>(o => o.Purpose == OtpPurpose.PasswordReset), It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(x => x.SendPasswordResetOtpAsync("user@uni.edu", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResendResetOtpAsync_UnregisteredEmail_ReturnsWithNoSideEffects()
    {
        _identityService.Setup(x => x.FindUserByEmailAsync("unknown@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await _sut.ResendResetOtpAsync(new ResendResetOtpRequest { Email = "unknown@uni.edu" });

        _otpRepository.Verify(x => x.AddAsync(It.IsAny<OtpRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(x => x.SendPasswordResetOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendResetOtpAsync_UnverifiedUser_ReturnsWithNoSideEffects()
    {
        _identityService.Setup(x => x.FindUserByEmailAsync("unverified@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-unv");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-unv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.ResendResetOtpAsync(new ResendResetOtpRequest { Email = "unverified@uni.edu" });

        _otpRepository.Verify(x => x.AddAsync(It.IsAny<OtpRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(x => x.SendPasswordResetOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendResetOtpAsync_EmailSendFailure_CompletesSuccessfully()
    {
        _identityService.Setup(x => x.FindUserByEmailAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-r2");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-r2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _emailService.Setup(x => x.SendPasswordResetOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP failure"));

        await _sut.ResendResetOtpAsync(new ResendResetOtpRequest { Email = "user@uni.edu" });
    }

    [Fact]
    public async Task ResendResetOtpAsync_RegisteredEmail_LogsMessageContainingResent()
    {
        _identityService.Setup(x => x.FindUserByEmailAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-r3");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-r3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.ResendResetOtpAsync(new ResendResetOtpRequest { Email = "user@uni.edu" });

        _logger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("resent")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ── ResetPasswordAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task ResetPasswordAsync_ValidOtpAndPassword_ConsumesOtpRevokesSessionsReturnsTokens()
    {
        var otp = "123456";
        var hash = ComputeHash(otp);
        var record = BuildOtpRecord("uid-rp1", hash, OtpPurpose.PasswordReset);

        _identityService.Setup(x => x.FindUserByEmailAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-rp1");
        _otpRepository.Setup(x => x.GetLatestOtpAsync("uid-rp1", OtpPurpose.PasswordReset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        _identityService.Setup(x => x.ResetPasswordAsync("uid-rp1", "NewPass2@", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _identityService.Setup(x => x.GetUserEmailAsync("uid-rp1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("user@uni.edu");
        _identityService.Setup(x => x.GetUserRoleAsync("uid-rp1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Student);

        var request = new ResetPasswordRequest { Email = "user@uni.edu", Otp = otp, NewPassword = "NewPass2@" };
        var (response, rawRefreshToken) = await _sut.ResetPasswordAsync(request);

        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal("raw-refresh-token", rawRefreshToken);
        _refreshTokenRepository.Verify(x => x.RevokeAllForUserAsync("uid-rp1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_NoOtpRequested_ThrowsBadRequest()
    {
        _identityService.Setup(x => x.FindUserByEmailAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-rp2");
        _otpRepository.Setup(x => x.GetLatestOtpAsync("uid-rp2", OtpPurpose.PasswordReset, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OtpRecord?)null);

        var request = new ResetPasswordRequest { Email = "user@uni.edu", Otp = "123456", NewPassword = "NewPass2@" };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _sut.ResetPasswordAsync(request));
        Assert.Equal("No password reset was requested.", ex.Message);
    }

    [Fact]
    public async Task ResetPasswordAsync_MaxAttemptsExceeded_ThrowsBadRequest()
    {
        var record = BuildLockedRecord("uid-rp3", OtpPurpose.PasswordReset, failedAttempts: 5);

        _identityService.Setup(x => x.FindUserByEmailAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-rp3");
        _otpRepository.Setup(x => x.GetLatestOtpAsync("uid-rp3", OtpPurpose.PasswordReset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var request = new ResetPasswordRequest { Email = "user@uni.edu", Otp = "999999", NewPassword = "NewPass2@" };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _sut.ResetPasswordAsync(request));
        Assert.Equal("Maximum OTP attempts exceeded. Please request a new code.", ex.Message);
    }

    [Fact]
    public async Task ResetPasswordAsync_OtpAlreadyUsed_ThrowsBadRequest()
    {
        var record = BuildUsedRecord("uid-rp4", OtpPurpose.PasswordReset);

        _identityService.Setup(x => x.FindUserByEmailAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-rp4");
        _otpRepository.Setup(x => x.GetLatestOtpAsync("uid-rp4", OtpPurpose.PasswordReset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var request = new ResetPasswordRequest { Email = "user@uni.edu", Otp = "123456", NewPassword = "NewPass2@" };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _sut.ResetPasswordAsync(request));
        Assert.Equal("No password reset was requested.", ex.Message);
    }

    [Fact]
    public async Task ResetPasswordAsync_ExpiredOtp_ThrowsBadRequest()
    {
        var record = BuildExpiredRecord("uid-rp5", OtpPurpose.PasswordReset);

        _identityService.Setup(x => x.FindUserByEmailAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-rp5");
        _otpRepository.Setup(x => x.GetLatestOtpAsync("uid-rp5", OtpPurpose.PasswordReset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var request = new ResetPasswordRequest { Email = "user@uni.edu", Otp = "123456", NewPassword = "NewPass2@" };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _sut.ResetPasswordAsync(request));
        Assert.Equal("The reset code has expired.", ex.Message);
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidOtp_RecordsFailedAttemptAndThrows()
    {
        var record = BuildOtpRecord("uid-rp6", ComputeHash("correct"), OtpPurpose.PasswordReset);

        _identityService.Setup(x => x.FindUserByEmailAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-rp6");
        _otpRepository.Setup(x => x.GetLatestOtpAsync("uid-rp6", OtpPurpose.PasswordReset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var request = new ResetPasswordRequest { Email = "user@uni.edu", Otp = "000000", NewPassword = "NewPass2@" };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _sut.ResetPasswordAsync(request));
        Assert.Equal("The reset code is incorrect.", ex.Message);
        _otpRepository.Verify(x => x.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, record.FailedAttempts);
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidPassword_DoesNotConsumeOtp()
    {
        var otp = "123456";
        var record = BuildOtpRecord("uid-rp7", ComputeHash(otp), OtpPurpose.PasswordReset);

        _identityService.Setup(x => x.FindUserByEmailAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-rp7");
        _otpRepository.Setup(x => x.GetLatestOtpAsync("uid-rp7", OtpPurpose.PasswordReset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        _identityService.Setup(x => x.ResetPasswordAsync("uid-rp7", "weak", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new Dictionary<string, string[]>
            {
                ["newPassword"] = ["Password too weak."]
            }));

        var request = new ResetPasswordRequest { Email = "user@uni.edu", Otp = otp, NewPassword = "weak" };

        await Assert.ThrowsAsync<ValidationException>(() => _sut.ResetPasswordAsync(request));

        Assert.False(record.IsUsed);
    }

    // ── LogoutAllDevicesAsync ───────────────────────────────────────────────

    [Fact]
    public async Task LogoutAllDevicesAsync_ValidUser_RevokesAllSessions()
    {
        await _sut.LogoutAllDevicesAsync("uid-l1");

        _refreshTokenRepository.Verify(x => x.RevokeAllForUserAsync("uid-l1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogoutAllDevicesAsync_ValidUser_LogsAuditEventWithUserId()
    {
        await _sut.LogoutAllDevicesAsync("uid-l2");

        _logger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("uid-l2")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LogoutAllDevicesAsync_ValidUser_IssuesNoNewTokens()
    {
        await _sut.LogoutAllDevicesAsync("uid-l3");

        _tokenService.Verify(x => x.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _tokenService.Verify(x => x.GenerateRefreshToken(), Times.Never);
    }

    // ── Transaction safety ──────────────────────────────────────────────────

    [Fact]
    public async Task ResetPasswordAsync_WhenIdentityServiceThrows_PropagatesException()
    {
        var otp = "123456";
        var record = BuildOtpRecord("uid-tx1", ComputeHash(otp), OtpPurpose.PasswordReset);

        _identityService.Setup(x => x.FindUserByEmailAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-tx1");
        _otpRepository.Setup(x => x.GetLatestOtpAsync("uid-tx1", OtpPurpose.PasswordReset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        _identityService.Setup(x => x.ResetPasswordAsync("uid-tx1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var request = new ResetPasswordRequest { Email = "user@uni.edu", Otp = otp, NewPassword = "NewP@ss1" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ResetPasswordAsync(request));
    }

    [Fact]
    public async Task RefreshAsync_WhenAddNewTokenThrows_PropagatesException()
    {
        var tokenInfo = new RefreshTokenInfo(Guid.NewGuid(), "uid-tx2", IsRevoked: false, DateTimeOffset.UtcNow.AddDays(6));

        _refreshTokenRepository.Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenInfo);
        _identityService.Setup(x => x.GetUserRoleAsync("uid-tx2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Student);
        _identityService.Setup(x => x.GetUserEmailAsync("uid-tx2", It.IsAny<CancellationToken>()))
            .ReturnsAsync("user@uni.edu");
        _refreshTokenRepository.Setup(x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _refreshTokenRepository.Setup(x => x.AddAsync(It.IsAny<string>(), "uid-tx2", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RefreshAsync("old-raw-token"));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static OtpRecord BuildOtpRecord(string userId, string hash, OtpPurpose purpose)
    {
        return OtpRecord.Create(userId, hash,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(9),
            purpose);
    }

    private static OtpRecord BuildLockedRecord(string userId, OtpPurpose purpose, int failedAttempts)
    {
        var record = OtpRecord.Create(userId, "somehash",
            DateTimeOffset.UtcNow.AddMinutes(-11),
            DateTimeOffset.UtcNow.AddMinutes(-1),
            purpose);
        for (int i = 0; i < failedAttempts; i++)
            record.RecordFailedAttempt(5, DateTimeOffset.UtcNow.AddHours(1));
        record.Invalidate();
        return record;
    }

    private static OtpRecord BuildUsedRecord(string userId, OtpPurpose purpose)
    {
        var record = OtpRecord.Create(userId, "somehash",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(5),
            purpose);
        record.Consume();
        return record;
    }

    private static OtpRecord BuildExpiredRecord(string userId, OtpPurpose purpose)
    {
        return OtpRecord.Create(userId, "somehash",
            DateTimeOffset.UtcNow.AddMinutes(-20),
            DateTimeOffset.UtcNow.AddMinutes(-10),
            purpose);
    }

    private static string ComputeHash(string otp)
    {
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(otp));
        return Convert.ToHexString(bytes);
    }

    // ── CreateAccountAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateAccountAsync_ValidAdminCallerStudentRole_ReturnsCreatedResponse()
    {
        var request = new CreateAccountRequest
        {
            Email = "student@uni.edu",
            FirstName = "Alice",
            LastName = "Smith",
            Password = "test-only",
            Role = "Student",
            InstitutionalId = "STU-001"
        };

        SetupCreateAccountHappyPath(request, "user-id-123");

        var result = await _sut.CreateAccountAsync(request, Roles.Admin);

        Assert.Equal("user-id-123", result.UserId);
        Assert.Equal(request.Email, result.Email);
        Assert.Equal("Student", result.Role);
        Assert.False(result.IsVerified);
    }

    [Fact]
    public async Task CreateAccountAsync_DuplicateEmail_ThrowsConflictException()
    {
        var request = new CreateAccountRequest
        {
            Email = "existing@uni.edu",
            FirstName = "Bob",
            LastName = "Jones",
            Password = "test-only",
            Role = "Faculty",
            InstitutionalId = "FAC-001"
        };

        _identityService.Setup(x => x.FindUserByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync("existing-user-id");

        await Assert.ThrowsAsync<ConflictException>(() => _sut.CreateAccountAsync(request, Roles.Admin));
    }

    [Fact]
    public async Task CreateAccountAsync_DuplicateInstitutionalId_ThrowsConflictException()
    {
        var request = new CreateAccountRequest
        {
            Email = "new@uni.edu",
            FirstName = "Carol",
            LastName = "Lee",
            Password = "test-only",
            Role = "Librarian",
            InstitutionalId = "LIB-001"
        };

        _identityService.Setup(x => x.FindUserByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _identityService.Setup(x => x.FindUserByInstitutionalIdAsync(request.InstitutionalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("other-user-id");

        await Assert.ThrowsAsync<ConflictException>(() => _sut.CreateAccountAsync(request, Roles.Admin));
    }

    [Fact]
    public async Task CreateAccountAsync_ValidRequest_SendsOtpEmail()
    {
        var request = new CreateAccountRequest
        {
            Email = "student@uni.edu",
            FirstName = "Alice",
            LastName = "Smith",
            Password = "test-only",
            Role = "Student",
            InstitutionalId = "STU-002"
        };

        SetupCreateAccountHappyPath(request, "user-id-456");

        await _sut.CreateAccountAsync(request, Roles.Admin);

        _emailService.Verify(
            x => x.SendRegistrationOtpAsync(request.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAccountAsync_ValidRequest_InvalidatesExistingOtpsAndAddsNew()
    {
        var request = new CreateAccountRequest
        {
            Email = "student@uni.edu",
            FirstName = "Alice",
            LastName = "Smith",
            Password = "test-only",
            Role = "Student",
            InstitutionalId = "STU-003"
        };

        SetupCreateAccountHappyPath(request, "user-id-789");

        await _sut.CreateAccountAsync(request, Roles.Admin);

        _otpRepository.Verify(
            x => x.InvalidateAllForUserAsync("user-id-789", It.IsAny<OtpPurpose>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _otpRepository.Verify(
            x => x.AddAsync(It.Is<OtpRecord>(o => o.UserId == "user-id-789" && !o.IsInvalidated),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAccountAsync_ValidRequest_OtpExpiresInTenMinutes()
    {
        var before = DateTimeOffset.UtcNow;
        var request = new CreateAccountRequest
        {
            Email = "student@uni.edu",
            FirstName = "Alice",
            LastName = "Smith",
            Password = "test-only",
            Role = "Student",
            InstitutionalId = "STU-004"
        };

        SetupCreateAccountHappyPath(request, "user-id-otp");

        OtpRecord? capturedOtp = null;
        _otpRepository.Setup(x => x.AddAsync(It.IsAny<OtpRecord>(), It.IsAny<CancellationToken>()))
            .Callback<OtpRecord, CancellationToken>((otp, _) => capturedOtp = otp)
            .Returns(Task.CompletedTask);

        await _sut.CreateAccountAsync(request, Roles.Admin);

        Assert.NotNull(capturedOtp);
        Assert.True(capturedOtp!.ExpiresAt >= before.AddMinutes(9).AddSeconds(55));
        Assert.True(capturedOtp.ExpiresAt <= before.AddMinutes(10).AddSeconds(5));
    }

    [Fact]
    public async Task CreateAccountAsync_LibrarianCallerCreatesStudent_ReturnsSuccess()
    {
        var request = new CreateAccountRequest
        {
            Email = "stu@uni.edu",
            FirstName = "Dan",
            LastName = "Fox",
            Password = "test-only",
            Role = "Student",
            InstitutionalId = "STU-005"
        };

        SetupCreateAccountHappyPath(request, "user-lib-stu");

        var result = await _sut.CreateAccountAsync(request, Roles.Librarian);

        Assert.Equal("user-lib-stu", result.UserId);
    }

    [Fact]
    public async Task CreateAccountAsync_LibrarianCallerCreatesFaculty_ReturnsSuccess()
    {
        var request = new CreateAccountRequest
        {
            Email = "fac@uni.edu",
            FirstName = "Eve",
            LastName = "Stone",
            Password = "test-only",
            Role = "Faculty",
            InstitutionalId = "FAC-005"
        };

        SetupCreateAccountHappyPath(request, "user-lib-fac");

        var result = await _sut.CreateAccountAsync(request, Roles.Librarian);

        Assert.Equal("user-lib-fac", result.UserId);
    }

    [Fact]
    public async Task CreateAccountAsync_LibrarianCallerCreatesLibrarian_ThrowsForbiddenException()
    {
        var request = new CreateAccountRequest
        {
            Email = "lib2@uni.edu",
            FirstName = "Frank",
            LastName = "Hill",
            Password = "test-only",
            Role = "Librarian",
            InstitutionalId = "LIB-005"
        };

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _sut.CreateAccountAsync(request, Roles.Librarian));
    }

    // ── VerifyOtpAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyOtpAsync_CorrectOtp_SetsAccountVerified()
    {
        var otp = "123456";
        var hash = ComputeHash(otp);
        var record = BuildRegistrationOtpRecord("uid-v1", hash);

        _identityService.Setup(x => x.ResolveUserIdAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-v1");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _otpRepository.Setup(x => x.GetActiveOtpAsync("uid-v1", It.IsAny<OtpPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        await _sut.VerifyOtpAsync(new VerifyOtpRequest { Identity = "user@uni.edu", Otp = otp });

        _identityService.Verify(x => x.SetVerifiedAsync("uid-v1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyOtpAsync_WrongOtp_ThrowsValidationExceptionAndIncrementsAttempts()
    {
        var record = BuildRegistrationOtpRecord("uid-v2", ComputeHash("654321"));

        _identityService.Setup(x => x.ResolveUserIdAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-v2");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-v2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _otpRepository.Setup(x => x.GetActiveOtpAsync("uid-v2", It.IsAny<OtpPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.VerifyOtpAsync(new VerifyOtpRequest { Identity = "user@uni.edu", Otp = "000000" }));

        Assert.Equal(1, record.FailedAttempts);
        _otpRepository.Verify(x => x.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyOtpAsync_FiveFailedAttempts_ThrowsLockedValidationException()
    {
        var record = BuildRegistrationOtpRecord("uid-v3", ComputeHash("654321"), failedAttempts: 5);

        _identityService.Setup(x => x.ResolveUserIdAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-v3");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-v3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _otpRepository.Setup(x => x.GetActiveOtpAsync("uid-v3", It.IsAny<OtpPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.VerifyOtpAsync(new VerifyOtpRequest { Identity = "user@uni.edu", Otp = "123456" }));
    }

    [Fact]
    public async Task VerifyOtpAsync_AlreadyVerified_ThrowsConflictException()
    {
        _identityService.Setup(x => x.ResolveUserIdAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-v4");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-v4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => _sut.VerifyOtpAsync(new VerifyOtpRequest { Identity = "user@uni.edu", Otp = "123456" }));
    }

    [Fact]
    public async Task VerifyOtpAsync_UserNotFound_ThrowsNotFoundException()
    {
        _identityService.Setup(x => x.ResolveUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.VerifyOtpAsync(new VerifyOtpRequest { Identity = "unknown@uni.edu", Otp = "123456" }));
    }

    // ── ResendOtpAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ResendOtpAsync_UnverifiedAccount_InvalidatesOldAndSendsNew()
    {
        _identityService.Setup(x => x.ResolveUserIdAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-r1");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _identityService.Setup(x => x.GetUserEmailAsync("uid-r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("user@uni.edu");

        await _sut.ResendOtpAsync(new ResendOtpRequest { Identity = "user@uni.edu" });

        _otpRepository.Verify(x => x.InvalidateAllForUserAsync("uid-r1", It.IsAny<OtpPurpose>(), It.IsAny<CancellationToken>()), Times.Once);
        _otpRepository.Verify(x => x.AddAsync(It.IsAny<OtpRecord>(), It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(x => x.SendRegistrationOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResendOtpAsync_AlreadyVerified_ThrowsConflictException()
    {
        _identityService.Setup(x => x.ResolveUserIdAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-r2");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-r2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => _sut.ResendOtpAsync(new ResendOtpRequest { Identity = "user@uni.edu" }));
    }

    [Fact]
    public async Task ResendOtpAsync_UserNotFound_ReturnsWithoutException()
    {
        _identityService.Setup(x => x.ResolveUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await _sut.ResendOtpAsync(new ResendOtpRequest { Identity = "unknown@uni.edu" });
    }

    private void SetupCreateAccountHappyPath(CreateAccountRequest request, string userId)
    {
        _identityService.Setup(x => x.FindUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _identityService.Setup(x => x.FindUserByInstitutionalIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _identityService.Setup(x => x.CreateUserAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Domain.Enums.ProfileRole>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
    }

    private static OtpRecord BuildRegistrationOtpRecord(string userId, string hash, int failedAttempts = 0)
    {
        var record = OtpRecord.Create(userId, hash,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(9),
            OtpPurpose.Registration);
        for (int i = 0; i < failedAttempts; i++)
            record.RecordFailedAttempt(5, DateTimeOffset.UtcNow.AddHours(1));
        return record;
    }
}
