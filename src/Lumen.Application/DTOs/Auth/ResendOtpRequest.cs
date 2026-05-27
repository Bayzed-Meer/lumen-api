using System.ComponentModel.DataAnnotations;

namespace Lumen.Application.DTOs.Auth;

public sealed record ResendOtpRequest
{
    [Required]
    public required string Identity { get; init; }
}
