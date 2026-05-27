using System.ComponentModel.DataAnnotations;

namespace Lumen.Infrastructure.Settings;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public required string Issuer { get; init; }

    [Required]
    public required string Audience { get; init; }

    [Required]
    [MinLength(32)]
    public required string Key { get; init; }

    [Range(1, int.MaxValue)]
    public required int AccessTokenExpiryMinutes { get; init; }
}
