using Lumen.Application.Common.Constants;
using Lumen.Application.Common.Exceptions;
using Lumen.Application.Common.Helpers;
using Lumen.Application.Common.Interfaces;
using Lumen.Application.DTOs.Auth;
using Lumen.Domain.Entities;
using Lumen.Domain.Enums;

namespace Lumen.Application.Services.Auth;

public sealed class RegisterService(
    IIdentityService identityService,
    IOtpRepository otpRepository,
    IEmailService emailService) : IRegisterService
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

        string userId = await identityService.CreateUserAsync(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password,
            role,
            request.InstitutionalId,
            ct);

        string otp = AuthCrypto.GenerateOtp();
        DateTimeOffset issuedAt = DateTimeOffset.UtcNow;

        await otpRepository.InvalidateAllForUserAsync(userId, ct);
        await otpRepository.AddAsync(
            OtpRecord.Create(userId, AuthCrypto.HashSecret(otp), issuedAt, issuedAt.AddMinutes(OtpExpiryMinutes)),
            ct);

        await emailService.SendOtpEmailAsync(request.Email, otp, ct);

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

        OtpRecord otpRecord = await otpRepository.GetActiveOtpAsync(userId, ct)
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

            string message = remaining <= 0
                ? $"Invalid verification code. Account is now locked. Please request a new code in {LockoutDurationHours * 60} minutes."
                : $"Invalid verification code. {remaining} attempt{(remaining == 1 ? "" : "s")} remaining.";

            throw new ValidationException(new Dictionary<string, string[]> { ["otp"] = [message] });
        }

        otpRecord.Invalidate();
        await otpRepository.UpdateAsync(otpRecord, ct);
        await identityService.SetVerifiedAsync(userId, ct);
    }

    public async Task ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default)
    {
        string? userId = await identityService.ResolveUserIdAsync(request.Identity, ct);
        if (userId is null)
            return;

        bool isVerified = await identityService.IsVerifiedAsync(userId, ct);
        if (isVerified)
            throw new ConflictException("Account is already verified.");

        OtpRecord? activeOtp = await otpRepository.GetActiveOtpAsync(userId, ct);

        if (activeOtp is not null && activeOtp.FailedAttempts >= MaxFailedAttempts)
            ThrowLockedException(activeOtp.LockedUntil, "identity");

        DateTimeOffset? lastIssuedAt = await otpRepository.GetLastIssuedAtAsync(userId, ct);
        if (lastIssuedAt is not null && DateTimeOffset.UtcNow - lastIssuedAt.Value < TimeSpan.FromMinutes(ResendCooldownMinutes))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["identity"] = ["Please wait before requesting a new verification code."]
            });

        string otp = AuthCrypto.GenerateOtp();
        DateTimeOffset issuedAt = DateTimeOffset.UtcNow;

        await otpRepository.InvalidateAllForUserAsync(userId, ct);
        await otpRepository.AddAsync(
            OtpRecord.Create(userId, AuthCrypto.HashSecret(otp), issuedAt, issuedAt.AddMinutes(OtpExpiryMinutes)),
            ct);

        string email = await identityService.GetUserEmailAsync(userId, ct);
        await emailService.SendOtpEmailAsync(email, otp, ct);
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
}
