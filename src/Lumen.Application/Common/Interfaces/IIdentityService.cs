using Lumen.Domain.Enums;

namespace Lumen.Application.Common.Interfaces;

public interface IIdentityService
{
    /// <summary>Atomically creates an ApplicationUser and the matching profile entity.</summary>
    Task<string> CreateUserAsync(
        string email,
        string firstName,
        string lastName,
        string password,
        ProfileRole role,
        string institutionalId,
        CancellationToken ct = default);

    Task<string?> FindUserByEmailAsync(string email, CancellationToken ct = default);
    Task<string?> FindUserByInstitutionalIdAsync(string institutionalId, CancellationToken ct = default);
    Task<string?> ResolveUserIdAsync(string identity, CancellationToken ct = default);
    Task<string> GetUserEmailAsync(string userId, CancellationToken ct = default);
    Task<bool> CheckPasswordAsync(string userId, string password, CancellationToken ct = default);
    Task<UserRole> GetUserRoleAsync(string userId, CancellationToken ct = default);
    Task<bool> IsVerifiedAsync(string userId, CancellationToken ct = default);
    Task SetVerifiedAsync(string userId, CancellationToken ct = default);
    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task ResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default);
}
