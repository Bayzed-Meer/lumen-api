using System.ComponentModel.DataAnnotations;

namespace Lumen.Application.DTOs.Auth;

public sealed record ResetPasswordRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Otp { get; init; }

    [Required]
    public required string NewPassword { get; init; }
}
