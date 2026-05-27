using System.ComponentModel.DataAnnotations;

namespace Lumen.Infrastructure.Identity;

public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    [Required]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }

    [Required]
    public required string FirstName { get; init; }

    [Required]
    public required string LastName { get; init; }
}
