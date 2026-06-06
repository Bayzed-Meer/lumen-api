using Lumen.Application.Common.Interfaces;
using Lumen.Infrastructure.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Lumen.Infrastructure.Services;

public sealed class SmtpEmailService(IOptions<SmtpSettings> options) : IEmailService
{
    public Task SendRegistrationOtpAsync(string recipient, string otp, CancellationToken ct = default) =>
        SendAsync(recipient, "Your Lumen verification code", otp, ct);

    public Task SendPasswordResetOtpAsync(string recipient, string otp, CancellationToken ct = default) =>
        SendAsync(recipient, "Your Lumen password reset code", otp, ct);

    private async Task SendAsync(string recipient, string subject, string otp, CancellationToken ct)
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress(options.Value.FromName, options.Value.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;

        message.Body = new TextPart("plain")
        {
            Text = $"Your one-time code is: {otp}\n\nThis code expires in 10 minutes."
        };

        using SmtpClient client = new();
        SecureSocketOptions tls = options.Value.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(options.Value.Host, options.Value.Port, tls, ct).ConfigureAwait(false);
        if (options.Value.UseTls)
            await client.AuthenticateAsync(options.Value.Username, options.Value.Password, ct).ConfigureAwait(false);
        await client.SendAsync(message, ct).ConfigureAwait(false);
        await client.DisconnectAsync(true, ct).ConfigureAwait(false);
    }
}
