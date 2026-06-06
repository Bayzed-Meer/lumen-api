using System.ComponentModel.DataAnnotations;

namespace Lumen.Application.DTOs.Auth;

public sealed record ResendResetOtpRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; init; }
}
