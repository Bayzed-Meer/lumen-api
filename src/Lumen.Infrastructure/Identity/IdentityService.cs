using Lumen.Application.Common.Exceptions;
using Lumen.Application.Common.Interfaces;
using Lumen.Domain.Entities;
using Lumen.Domain.Enums;
using Lumen.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lumen.Infrastructure.Identity;

public sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    AppDbContext dbContext) : IIdentityService
{
    public async Task<string> CreateUserAsync(
        string email,
        string firstName,
        string lastName,
        string password,
        ProfileRole role,
        string institutionalId,
        CancellationToken ct = default)
    {
        ApplicationUser user = new()
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            IsVerified = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        IdentityResult result = await userManager.CreateAsync(user, password).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = result.Errors.ToDictionary(
                e => e.Code,
                e => new[] { e.Description });
            throw new ValidationException(errors);
        }

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, role.ToString()).ConfigureAwait(false);
        if (!roleResult.Succeeded)
        {
            var errors = roleResult.Errors.ToDictionary(
                e => e.Code,
                e => new[] { e.Description });
            throw new ValidationException(errors);
        }

        switch (role)
        {
            case ProfileRole.Student:
                dbContext.Students.Add(new Student { UserId = user.Id, InstitutionalId = institutionalId });
                break;
            case ProfileRole.Faculty:
                dbContext.Faculty.Add(new Faculty { UserId = user.Id, InstitutionalId = institutionalId });
                break;
            case ProfileRole.Librarian:
                dbContext.Librarians.Add(new Librarian { UserId = user.Id, InstitutionalId = institutionalId });
                break;
            default:
                throw new InvalidOperationException($"Unhandled ProfileRole: {role}");
        }

        return user.Id;
    }

    public async Task<bool> IsVerifiedAsync(string userId, CancellationToken ct = default)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.IsVerified)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task SetVerifiedAsync(string userId, CancellationToken ct = default)
    {
        ApplicationUser user = await userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new NotFoundException("User", userId);

        user.IsVerified = true;

        IdentityResult result = await userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
            throw new ValidationException(errors);
        }
    }

    public async Task<string?> ResolveUserIdAsync(string identity, CancellationToken ct = default)
    {
        string? userId = await FindUserByEmailAsync(identity, ct).ConfigureAwait(false);
        if (userId is not null)
            return userId;

        return await FindUserByInstitutionalIdAsync(identity, ct).ConfigureAwait(false);
    }

    public async Task<string?> FindUserByEmailAsync(string email, CancellationToken ct = default)
    {
        string normalizedEmail = email.ToUpperInvariant();
        string? userId = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.NormalizedEmail == normalizedEmail)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return userId;
    }

    public async Task<string?> FindUserByInstitutionalIdAsync(string institutionalId, CancellationToken ct = default)
    {
        string? studentUserId = await dbContext.Students
            .AsNoTracking()
            .Where(s => s.InstitutionalId == institutionalId)
            .Select(s => s.UserId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (studentUserId is not null)
            return studentUserId;

        string? facultyUserId = await dbContext.Faculty
            .AsNoTracking()
            .Where(f => f.InstitutionalId == institutionalId)
            .Select(f => f.UserId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (facultyUserId is not null)
            return facultyUserId;

        string? librarianUserId = await dbContext.Librarians
            .AsNoTracking()
            .Where(l => l.InstitutionalId == institutionalId)
            .Select(l => l.UserId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return librarianUserId;
    }

    public async Task<string> GetUserEmailAsync(string userId, CancellationToken ct = default)
    {
        string? email = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return email ?? throw new NotFoundException("User", userId);
    }

    public async Task<UserRole> GetUserRoleAsync(string userId, CancellationToken ct = default)
    {
        string? roleName = await dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId)
            .Join(dbContext.Roles,
                userRole => userRole.RoleId,
                identityRole => identityRole.Id,
                (userRole, identityRole) => identityRole.Name)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (roleName is null || !Enum.TryParse(roleName, out UserRole role))
            throw new NotFoundException("User", userId);

        return role;
    }

    public async Task<bool> CheckPasswordAsync(string userId, string password, CancellationToken ct = default)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
            return false;

        return await userManager.CheckPasswordAsync(user, password).ConfigureAwait(false);
    }

    public async Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        ApplicationUser user = await userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new NotFoundException("User", userId);

        IdentityResult result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
            throw new ValidationException(errors);
        }
    }

    public async Task ResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default)
    {
        ApplicationUser user = await userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new NotFoundException("User", userId);

        await userManager.RemovePasswordAsync(user).ConfigureAwait(false);
        IdentityResult result = await userManager.AddPasswordAsync(user, newPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
            throw new ValidationException(errors);
        }
    }
}
