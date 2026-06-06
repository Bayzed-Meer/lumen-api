using Lumen.Application.DTOs.Auth;

namespace Lumen.Application.Services.Auth;

public interface IAuthService
{
    Task<CreateAccountResponse> CreateAccountAsync(CreateAccountRequest request, string currentUserRole, CancellationToken ct = default);
    Task VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct = default);
    Task ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default);

    Task<(LoginResponse Response, string RawRefreshToken)> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default);
    Task<(LoginResponse Response, string NewRawRefreshToken)> RefreshAsync(string rawRefreshToken, CancellationToken ct = default);

    Task<(LoginResponse Response, string RawRefreshToken)> ChangePasswordAsync(
        string userId, ChangePasswordRequest request, CancellationToken ct = default);

    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);

    Task ResendResetOtpAsync(ResendResetOtpRequest request, CancellationToken ct = default);

    Task<(LoginResponse Response, string RawRefreshToken)> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken ct = default);

    Task LogoutAllDevicesAsync(string userId, CancellationToken ct = default);
}
