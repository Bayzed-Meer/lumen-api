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
    private const string AdminRole = "Admin";

    public async Task SeedAsync()
    {
        await EnsureRoleAsync();
        await EnsureAdminUserAsync();
    }

    private async Task EnsureRoleAsync()
    {
        if (await roleManager.RoleExistsAsync(AdminRole))
            return;

        IdentityResult result = await roleManager.CreateAsync(new IdentityRole(AdminRole));
        if (result.Succeeded)
            logger.LogInformation("Created role '{Role}'", AdminRole);
        else
            logger.LogError("Failed to create role '{Role}': {Errors}",
                AdminRole, string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    private async Task EnsureAdminUserAsync()
    {
        AdminSeedOptions seed = options.Value;
        if (await userManager.FindByEmailAsync(seed.Email) is not null)
            return;

        var user = new ApplicationUser
        {
            UserName = seed.Email,
            Email = seed.Email,
            EmailConfirmed = true,
        };

        IdentityResult createResult = await userManager.CreateAsync(user, seed.Password);
        if (!createResult.Succeeded)
        {
            logger.LogError("Failed to create admin user '{Email}': {Errors}",
                seed.Email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, AdminRole);
        if (roleResult.Succeeded)
            logger.LogInformation("Seeded admin user '{Email}' with role '{Role}'",
                seed.Email, AdminRole);
        else
            logger.LogError("Failed to assign role '{Role}' to '{Email}': {Errors}",
                AdminRole, seed.Email, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
    }
}
