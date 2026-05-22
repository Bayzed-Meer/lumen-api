namespace Lumen.Infrastructure.Identity;

public class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";
    public required string Email { get; init; }
    public required string Password { get; init; }
}
