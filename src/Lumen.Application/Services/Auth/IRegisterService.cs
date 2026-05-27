using Lumen.Application.DTOs.Auth;

namespace Lumen.Application.Services.Auth;

public interface IRegisterService
{
    Task<CreateAccountResponse> CreateAccountAsync(CreateAccountRequest request, string currentUserRole, CancellationToken ct = default);
    Task VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct = default);
    Task ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default);
}
