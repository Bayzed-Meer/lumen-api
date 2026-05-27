namespace Lumen.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendOtpEmailAsync(string recipient, string otp, CancellationToken ct = default);
}
