namespace Lumen.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendRegistrationOtpAsync(string recipient, string otp, CancellationToken ct = default);
    Task SendPasswordResetOtpAsync(string recipient, string otp, CancellationToken ct = default);
}
