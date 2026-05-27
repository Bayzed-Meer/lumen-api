using System.ComponentModel.DataAnnotations;

namespace Lumen.Application.DTOs.Auth;

public sealed record VerifyOtpRequest
{
    [Required]
    public required string Identity { get; init; }

    [Required]
    [RegularExpression(@"^\d{6}$")]
    public required string Otp { get; init; }
}
