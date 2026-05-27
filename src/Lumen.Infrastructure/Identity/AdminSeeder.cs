using Lumen.Application.Common.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lumen.Infrastructure.Identity;

public sealed class AdminSeeder(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<AdminSeedOptions> options,
    ILogger<AdminSeeder> logger)
{
    private static readonly string[] AllRoles =
    [
        Roles.Admin,
        Roles.Librarian,
        Roles.Student,
        Roles.Faculty,
    ];

    public async Task SeedAsync()
    {
        await EnsureAllRolesAsync();
        await EnsureAdminUserAsync();
    }

    private async Task EnsureAllRolesAsync()
    {
        foreach (string role in AllRoles)
        {
            if (await roleManager.RoleExistsAsync(role).ConfigureAwait(false))
                continue;

            IdentityResult result = await roleManager.CreateAsync(new IdentityRole(role)).ConfigureAwait(false);
            if (result.Succeeded)
                logger.LogInformation("Created role '{Role}'", role);
            else
                logger.LogError("Failed to create role '{Role}': {Errors}",
                role, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task EnsureAdminUserAsync()
    {
        AdminSeedOptions seed = options.Value;
        ApplicationUser? existing = await userManager.FindByEmailAsync(seed.Email).ConfigureAwait(false);

        if (existing is null)
        {
            ApplicationUser user = new()
            {
                UserName = seed.Email,
                Email = seed.Email,
                EmailConfirmed = true,
                FirstName = seed.FirstName,
                LastName = seed.LastName,
                IsVerified = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            IdentityResult createResult = await userManager.CreateAsync(user, seed.Password).ConfigureAwait(false);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to create admin user '{seed.Email}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");

            existing = user;
            logger.LogInformation("Created admin user '{Email}'", seed.Email);
        }

        bool hasAdminRole = await userManager.IsInRoleAsync(existing, Roles.Admin).ConfigureAwait(false);
        if (!hasAdminRole)
        {
            IdentityResult roleResult = await userManager.AddToRoleAsync(existing, Roles.Admin).ConfigureAwait(false);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to assign role '{Roles.Admin}' to '{seed.Email}': {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");

            logger.LogInformation("Assigned role '{Role}' to admin user '{Email}'", Roles.Admin, seed.Email);
        }
    }
}
