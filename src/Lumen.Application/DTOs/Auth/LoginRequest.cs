using System.ComponentModel.DataAnnotations;

namespace Lumen.Application.DTOs.Auth;

public sealed record LoginRequest
{
    [Required]
    public required string Identity { get; init; }

    [Required]
    public required string Password { get; init; }
}
