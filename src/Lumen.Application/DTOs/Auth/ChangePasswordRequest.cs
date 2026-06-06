using System.ComponentModel.DataAnnotations;

namespace Lumen.Application.DTOs.Auth;

public sealed record ChangePasswordRequest
{
    [Required]
    public required string CurrentPassword { get; init; }

    [Required]
    public required string NewPassword { get; init; }
}
