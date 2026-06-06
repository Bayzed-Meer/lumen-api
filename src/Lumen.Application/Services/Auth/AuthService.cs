using Lumen.Application.Common.Constants;
using Lumen.Application.Common.Exceptions;
using Lumen.Application.Common.Helpers;
using Lumen.Application.Common.Interfaces;
using Lumen.Application.Common.Settings;
using Lumen.Application.DTOs.Auth;
using Lumen.Domain.Entities;
using Lumen.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lumen.Application.Services.Auth;

public sealed class AuthService(
    IIdentityService identityService,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepository,
    IOtpRepository otpRepository,
    IEmailService emailService,
    IOptions<JwtSettings> jwtSettings,
    IUnitOfWork unitOfWork,
    ILogger<AuthService> logger) : IAuthService
{
    private const int OtpExpiryMinutes = 10;
    private const int MaxFailedAttempts = 5;
    private const int ResendCooldownMinutes = 1;
    private const int LockoutDurationHours = 1;

    public async Task<CreateAccountResponse> CreateAccountAsync(
        CreateAccountRequest request,
        string currentUserRole,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<ProfileRole>(request.Role, out ProfileRole role))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["role"] = [$"'{request.Role}' is not a valid role."]
            });

        if (currentUserRole == Roles.Librarian && request.Role == Roles.Librarian)
            throw new ForbiddenException("Librarians may not create accounts for other librarians.");

        string? existingByEmail = await identityService.FindUserByEmailAsync(request.Email, ct);
        if (existingByEmail is not null)
            throw new ConflictException($"Email '{request.Email}' is already registered.");

        string? existingByInstitutionalId = await identityService.FindUserByInstitutionalIdAsync(request.InstitutionalId, ct);
        if (existingByInstitutionalId is not null)
            throw new ConflictException($"Institutional ID '{request.InstitutionalId}' is already registered.");

        string otp = AuthCrypto.GenerateOtp();
        DateTimeOffset issuedAt = DateTimeOffset.UtcNow;

        string userId = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            string id = await identityService.CreateUserAsync(
                request.Email,
                request.FirstName,
                request.LastName,
                request.Password,
                role,
                request.InstitutionalId,
                ct);

            string otpHash = AuthCrypto.HashSecret(otp);
            DateTimeOffset expiresAt = issuedAt.AddMinutes(OtpExpiryMinutes);
            var otpRecord = OtpRecord.Create(id, otpHash, issuedAt, expiresAt, OtpPurpose.Registration);

            await otpRepository.InvalidateAllForUserAsync(id, OtpPurpose.Registration, ct);
            await otpRepository.AddAsync(otpRecord, ct);

            return id;
        }, ct);

        try
        {
            await emailService.SendRegistrationOtpAsync(request.Email, otp, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send registration OTP email to {Email}", request.Email);
        }

        return new CreateAccountResponse
        {
            UserId = userId,
            Email = request.Email,
            Role = request.Role,
            IsVerified = false
        };
    }

    public async Task VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct = default)
    {
        string userId = await identityService.ResolveUserIdAsync(request.Identity, ct)
            ?? throw new NotFoundException("User", request.Identity);

        bool isVerified = await identityService.IsVerifiedAsync(userId, ct);
        if (isVerified)
            throw new ConflictException("Account is already verified.");

        OtpRecord otpRecord = await otpRepository.GetActiveOtpAsync(userId, OtpPurpose.Registration, ct)
            ?? throw new ValidationException(new Dictionary<string, string[]>
            {
                ["otp"] = ["OTP has expired or does not exist. Please request a new code."]
            });

        if (otpRecord.FailedAttempts >= MaxFailedAttempts)
            ThrowLockedException(otpRecord.LockedUntil, "otp");

        string hash = AuthCrypto.HashSecret(request.Otp);
        if (!string.Equals(hash, otpRecord.CodeHash, StringComparison.OrdinalIgnoreCase))
        {
            int remaining = otpRecord.RecordFailedAttempt(MaxFailedAttempts, DateTimeOffset.UtcNow.AddHours(LockoutDurationHours));
            await otpRepository.UpdateAsync(otpRecord, ct);
            await unitOfWork.SaveChangesAsync(ct);

            string message = remaining <= 0
                ? $"Invalid verification code. Account is now locked. Please request a new code in {LockoutDurationHours * 60} minutes."
                : $"Invalid verification code. {remaining} attempt{(remaining == 1 ? "" : "s")} remaining.";

            throw new ValidationException(new Dictionary<string, string[]> { ["otp"] = [message] });
        }

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            otpRecord.Invalidate();
            await otpRepository.UpdateAsync(otpRecord, ct);
            await identityService.SetVerifiedAsync(userId, ct);
        }, ct);
    }

    public async Task ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default)
    {
        string? userId = await identityService.ResolveUserIdAsync(request.Identity, ct);
        if (userId is null)
            return;

        bool isVerified = await identityService.IsVerifiedAsync(userId, ct);
        if (isVerified)
            throw new ConflictException("Account is already verified.");

        OtpRecord? activeOtp = await otpRepository.GetActiveOtpAsync(userId, OtpPurpose.Registration, ct);

        if (activeOtp is not null && activeOtp.FailedAttempts >= MaxFailedAttempts)
            ThrowLockedException(activeOtp.LockedUntil, "identity");

        DateTimeOffset? lastIssuedAt = await otpRepository.GetLastIssuedAtAsync(userId, OtpPurpose.Registration, ct);
        if (lastIssuedAt is not null && DateTimeOffset.UtcNow - lastIssuedAt.Value < TimeSpan.FromMinutes(ResendCooldownMinutes))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["identity"] = ["Please wait before requesting a new verification code."]
            });

        string otp = AuthCrypto.GenerateOtp();
        DateTimeOffset issuedAt = DateTimeOffset.UtcNow;

        string otpHash = AuthCrypto.HashSecret(otp);
        DateTimeOffset expiresAt = issuedAt.AddMinutes(OtpExpiryMinutes);
        var otpRecord = OtpRecord.Create(userId, otpHash, issuedAt, expiresAt, OtpPurpose.Registration);

        await otpRepository.InvalidateAllForUserAsync(userId, OtpPurpose.Registration, ct);
        await otpRepository.AddAsync(otpRecord, ct);
        await unitOfWork.SaveChangesAsync(ct);

        string email = await identityService.GetUserEmailAsync(userId, ct);
        try
        {
            await emailService.SendRegistrationOtpAsync(email, otp, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send registration OTP email to {Email}", email);
        }
    }

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

        DateTimeOffset refreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(jwtSettings.Value.RefreshTokenExpiryDays);
        await refreshTokenRepository.AddAsync(tokenHash, userId, refreshTokenExpiry, ct: ct);
        await unitOfWork.SaveChangesAsync(ct);

        LoginResponse response = new() { AccessToken = accessToken, ExpiresInSeconds = jwtSettings.Value.AccessTokenExpiryMinutes * 60 };
        return (response, rawRefreshToken);
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
            await unitOfWork.SaveChangesAsync(ct);
            throw new UnauthorizedException("Refresh token reuse detected. All sessions revoked.");
        }

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

        await refreshTokenRepository.RevokeAsync(hash, ct);

        string newRawRefreshToken = tokenService.GenerateRefreshToken();
        string newTokenHash = AuthCrypto.HashSecret(newRawRefreshToken);
        DateTimeOffset newTokenExpiry = DateTimeOffset.UtcNow.AddDays(jwtSettings.Value.RefreshTokenExpiryDays);
        await refreshTokenRepository.AddAsync(newTokenHash, tokenInfo.UserId, newTokenExpiry, ct);

        string accessToken = tokenService.GenerateAccessToken(tokenInfo.UserId, email, roleName);
        LoginResponse response = new() { AccessToken = accessToken, ExpiresInSeconds = jwtSettings.Value.AccessTokenExpiryMinutes * 60 };

        await unitOfWork.SaveChangesAsync(ct);
        return (response, newRawRefreshToken);
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        string hash = AuthCrypto.HashSecret(rawRefreshToken);
        await refreshTokenRepository.RevokeAsync(hash, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task LogoutAllDevicesAsync(string userId, CancellationToken ct = default)
    {
        await refreshTokenRepository.RevokeAllForUserAsync(userId, ct);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("All sessions revoked for user {UserId}", userId);
    }

    public async Task<(LoginResponse Response, string RawRefreshToken)> ChangePasswordAsync(
        string userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        if (request.NewPassword == request.CurrentPassword)
            throw new ConflictException("New password must differ from current password.");

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await identityService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct);
            await otpRepository.InvalidateAllForUserAsync(userId, OtpPurpose.PasswordReset, ct);
            await refreshTokenRepository.RevokeAllForUserAsync(userId, ct);
            (LoginResponse response, string rawRefreshToken) = await IssueTokenPairAsync(userId, ct);
            logger.LogInformation("Password changed for user {UserId}", userId);
            return (response, rawRefreshToken);
        }, ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        await SendResetOtpAsync(request.Email, "sent", ct);
    }

    public async Task ResendResetOtpAsync(ResendResetOtpRequest request, CancellationToken ct = default)
    {
        await SendResetOtpAsync(request.Email, "resent", ct);
    }

    public async Task<(LoginResponse Response, string RawRefreshToken)> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken ct = default)
    {
        string userId = await identityService.FindUserByEmailAsync(request.Email, ct)
            ?? throw new UnauthorizedException("Invalid request.");

        OtpRecord otpRecord = await otpRepository.GetLatestOtpAsync(userId, OtpPurpose.PasswordReset, ct)
            ?? throw new BadRequestException("No password reset was requested.");

        if (otpRecord.IsInvalidated && otpRecord.FailedAttempts >= MaxFailedAttempts)
            throw new BadRequestException("Maximum OTP attempts exceeded. Please request a new code.");

        if (otpRecord.IsUsed)
            throw new BadRequestException("No password reset was requested.");

        if (otpRecord.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new BadRequestException("The reset code has expired.");

        string hash = AuthCrypto.HashSecret(request.Otp);
        if (!string.Equals(hash, otpRecord.CodeHash, StringComparison.OrdinalIgnoreCase))
        {
            DateTimeOffset lockoutExpiry = DateTimeOffset.UtcNow.AddHours(LockoutDurationHours);
            otpRecord.RecordFailedAttempt(MaxFailedAttempts, lockoutExpiry);
            await otpRepository.UpdateAsync(otpRecord, ct);
            await unitOfWork.SaveChangesAsync(ct);
            throw new BadRequestException("The reset code is incorrect.");
        }

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await identityService.ResetPasswordAsync(userId, request.NewPassword, ct);
            otpRecord.Consume();
            await otpRepository.UpdateAsync(otpRecord, ct);
            await otpRepository.InvalidateAllForUserAsync(userId, OtpPurpose.PasswordReset, ct);
            await refreshTokenRepository.RevokeAllForUserAsync(userId, ct);
            (LoginResponse response, string rawRefreshToken) = await IssueTokenPairAsync(userId, ct);
            logger.LogInformation("Password reset for user {UserId}", userId);
            return (response, rawRefreshToken);
        }, ct);
    }

    private async Task SendResetOtpAsync(string email, string auditVerb, CancellationToken ct)
    {
        string? userId = await identityService.FindUserByEmailAsync(email, ct);
        if (userId is null)
            return;

        bool isVerified = await identityService.IsVerifiedAsync(userId, ct);
        if (!isVerified)
            return;

        string otp = AuthCrypto.GenerateOtp();
        DateTimeOffset issuedAt = DateTimeOffset.UtcNow;

        string otpHash = AuthCrypto.HashSecret(otp);
        DateTimeOffset expiresAt = issuedAt.AddMinutes(OtpExpiryMinutes);
        var otpRecord = OtpRecord.Create(userId, otpHash, issuedAt, expiresAt, OtpPurpose.PasswordReset);

        await otpRepository.InvalidateAllForUserAsync(userId, OtpPurpose.PasswordReset, ct);
        await otpRepository.AddAsync(otpRecord, ct);
        await unitOfWork.SaveChangesAsync(ct);

        try
        {
            await emailService.SendPasswordResetOtpAsync(email, otp, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password reset OTP email to {Email}", email);
        }

        logger.LogInformation("Password reset OTP {Verb} to {Email}", auditVerb, email);
    }

    private static void ThrowLockedException(DateTimeOffset? lockedUntil, string fieldKey)
    {
        if (!lockedUntil.HasValue)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [fieldKey] = ["Account is locked due to too many failed attempts. Please request a new code."]
            });

        int minutesLeft = (int)Math.Ceiling((lockedUntil.Value - DateTimeOffset.UtcNow).TotalMinutes);
        throw new ValidationException(new Dictionary<string, string[]>
        {
            [fieldKey] = [$"Account is locked due to too many failed attempts. Please request a new code in {minutesLeft} minute{(minutesLeft == 1 ? "" : "s")}."]
        });
    }

    private async Task<(LoginResponse Response, string RawRefreshToken)> IssueTokenPairAsync(string userId, CancellationToken ct)
    {
        string email = await identityService.GetUserEmailAsync(userId, ct);
        UserRole role = await identityService.GetUserRoleAsync(userId, ct);

        string accessToken = tokenService.GenerateAccessToken(userId, email, role.ToString());
        string rawRefreshToken = tokenService.GenerateRefreshToken();
        string tokenHash = AuthCrypto.HashSecret(rawRefreshToken);

        DateTimeOffset refreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(jwtSettings.Value.RefreshTokenExpiryDays);
        await refreshTokenRepository.AddAsync(tokenHash, userId, refreshTokenExpiry, ct);

        LoginResponse response = new()
        {
            AccessToken = accessToken,
            ExpiresInSeconds = jwtSettings.Value.AccessTokenExpiryMinutes * 60
        };
        return (response, rawRefreshToken);
    }
}
