using Lumen.Application.DTOs.Auth;

namespace Lumen.Application.Services.Auth;

public interface IAuthService
{
    Task<(LoginResponse Response, string RawRefreshToken)> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default);
    Task<(LoginResponse Response, string NewRawRefreshToken)> RefreshAsync(string rawRefreshToken, CancellationToken ct = default);
}
