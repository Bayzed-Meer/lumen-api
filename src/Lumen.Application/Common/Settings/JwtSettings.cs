using System.ComponentModel.DataAnnotations;

namespace Lumen.Application.Common.Settings;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    [Range(1, int.MaxValue)]
    public required int AccessTokenExpiryMinutes { get; init; }

    [Range(1, int.MaxValue)]
    public required int RefreshTokenExpiryDays { get; init; }
}
