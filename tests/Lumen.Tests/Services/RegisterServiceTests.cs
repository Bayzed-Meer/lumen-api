using Lumen.Application.Common.Constants;
using Lumen.Application.Common.Exceptions;
using Lumen.Application.Common.Interfaces;
using Lumen.Application.DTOs.Auth;
using Lumen.Application.Services.Auth;
using Lumen.Domain.Entities;
using Moq;

namespace Lumen.Tests.Services;

public class RegisterServiceTests
{
    private readonly Mock<IIdentityService> _identityService = new();
    private readonly Mock<IOtpRepository> _otpRepository = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly IRegisterService _sut;

    public RegisterServiceTests()
    {
        _sut = new RegisterService(_identityService.Object, _otpRepository.Object, _emailService.Object);
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
            Password = "P@ssword1",
            Role = "Student",
            InstitutionalId = "STU-001"
        };

        _identityService.Setup(x => x.FindUserByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _identityService.Setup(x => x.FindUserByInstitutionalIdAsync(request.InstitutionalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _identityService.Setup(x => x.CreateUserAsync(
            request.Email, request.FirstName, request.LastName,
            request.Password, It.IsAny<Domain.Enums.ProfileRole>(), request.InstitutionalId,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("user-id-123");

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
            Password = "P@ssword1",
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
            Password = "P@ssword1",
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
            Password = "P@ssword1",
            Role = "Student",
            InstitutionalId = "STU-002"
        };

        SetupHappyPath(request, "user-id-456");

        await _sut.CreateAccountAsync(request, Roles.Admin);

        _emailService.Verify(
            x => x.SendOtpEmailAsync(request.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()),
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
            Password = "P@ssword1",
            Role = "Student",
            InstitutionalId = "STU-003"
        };

        SetupHappyPath(request, "user-id-789");

        await _sut.CreateAccountAsync(request, Roles.Admin);

        _otpRepository.Verify(
            x => x.InvalidateAllForUserAsync("user-id-789", It.IsAny<CancellationToken>()),
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
            Password = "P@ssword1",
            Role = "Student",
            InstitutionalId = "STU-004"
        };

        SetupHappyPath(request, "user-id-otp");

        OtpRecord? capturedOtp = null;
        _otpRepository.Setup(x => x.AddAsync(It.IsAny<OtpRecord>(), It.IsAny<CancellationToken>()))
            .Callback<OtpRecord, CancellationToken>((otp, _) => capturedOtp = otp)
            .Returns(Task.CompletedTask);

        await _sut.CreateAccountAsync(request, Roles.Admin);

        Assert.NotNull(capturedOtp);
        Assert.True(capturedOtp!.ExpiresAt >= before.AddMinutes(9).AddSeconds(55));
        Assert.True(capturedOtp.ExpiresAt <= before.AddMinutes(10).AddSeconds(5));
    }

    // ── Role enforcement (US2) ──────────────────────────────────────────────

    [Fact]
    public async Task CreateAccountAsync_LibrarianCallerCreatesStudent_ReturnsSuccess()
    {
        var request = new CreateAccountRequest
        {
            Email = "stu@uni.edu",
            FirstName = "Dan",
            LastName = "Fox",
            Password = "P@ssword1",
            Role = "Student",
            InstitutionalId = "STU-005"
        };

        SetupHappyPath(request, "user-lib-stu");

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
            Password = "P@ssword1",
            Role = "Faculty",
            InstitutionalId = "FAC-005"
        };

        SetupHappyPath(request, "user-lib-fac");

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
            Password = "P@ssword1",
            Role = "Librarian",
            InstitutionalId = "LIB-005"
        };

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _sut.CreateAccountAsync(request, Roles.Librarian));
    }

    // ── VerifyOtpAsync (US3) ────────────────────────────────────────────────

    [Fact]
    public async Task VerifyOtpAsync_CorrectOtp_SetsAccountVerified()
    {
        var otp = "123456";
        var hash = ComputeHash(otp);
        var record = BuildOtpRecord("uid-v1", hash);

        _identityService.Setup(x => x.ResolveUserIdAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-v1");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _otpRepository.Setup(x => x.GetActiveOtpAsync("uid-v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        await _sut.VerifyOtpAsync(new VerifyOtpRequest { Identity = "user@uni.edu", Otp = otp });

        _identityService.Verify(x => x.SetVerifiedAsync("uid-v1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyOtpAsync_WrongOtp_ThrowsValidationExceptionAndIncrementsAttempts()
    {
        var record = BuildOtpRecord("uid-v2", ComputeHash("654321"));

        _identityService.Setup(x => x.ResolveUserIdAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-v2");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-v2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _otpRepository.Setup(x => x.GetActiveOtpAsync("uid-v2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.VerifyOtpAsync(new VerifyOtpRequest { Identity = "user@uni.edu", Otp = "000000" }));

        Assert.Equal(1, record.FailedAttempts);
        _otpRepository.Verify(x => x.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyOtpAsync_FiveFailedAttempts_ThrowsLockedValidationException()
    {
        // BuildOtpRecord with 5 attempts automatically sets LockedUntil via RecordFailedAttempt
        var record = BuildOtpRecord("uid-v3", ComputeHash("654321"), failedAttempts: 5);

        _identityService.Setup(x => x.ResolveUserIdAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-v3");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-v3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _otpRepository.Setup(x => x.GetActiveOtpAsync("uid-v3", It.IsAny<CancellationToken>()))
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

    // ── ResendOtpAsync (US4) ────────────────────────────────────────────────

    [Fact]
    public async Task ResendOtpAsync_UnverifiedAccount_InvalidatesOldAndSendsNew()
    {
        _identityService.Setup(x => x.ResolveUserIdAsync("user@uni.edu", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uid-r1");
        _identityService.Setup(x => x.IsVerifiedAsync("uid-r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.ResendOtpAsync(new ResendOtpRequest { Identity = "user@uni.edu" });

        _otpRepository.Verify(x => x.InvalidateAllForUserAsync("uid-r1", It.IsAny<CancellationToken>()), Times.Once);
        _otpRepository.Verify(x => x.AddAsync(It.IsAny<OtpRecord>(), It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(x => x.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
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

    // ── Helpers ─────────────────────────────────────────────────────────────

    private void SetupHappyPath(CreateAccountRequest request, string userId)
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

    private static OtpRecord BuildOtpRecord(string userId, string hash, int failedAttempts = 0)
    {
        var record = OtpRecord.Create(userId, hash,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(9));
        for (int i = 0; i < failedAttempts; i++)
            record.RecordFailedAttempt(5, DateTimeOffset.UtcNow.AddHours(1));
        return record;
    }

    private static string ComputeHash(string otp)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(otp));
        return Convert.ToHexString(bytes);
    }
}
