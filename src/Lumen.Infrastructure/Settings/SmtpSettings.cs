using System.ComponentModel.DataAnnotations;

namespace Lumen.Infrastructure.Settings;

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    [Required]
    public required string Host { get; init; }

    [Required]
    [Range(1, 65535)]
    public required int Port { get; init; }

    [Required]
    public required string Username { get; init; }

    [Required]
    public required string Password { get; init; }

    [Required]
    [EmailAddress]
    public required string FromAddress { get; init; }

    [Required]
    public required string FromName { get; init; }

    public bool UseTls { get; init; } = true;
}
