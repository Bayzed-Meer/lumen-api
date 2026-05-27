namespace Lumen.Application.Common.Interfaces;

public sealed record RefreshTokenInfo(Guid Id, string UserId, bool IsRevoked, DateTimeOffset ExpiresAt);

public interface IRefreshTokenRepository
{
    Task AddAsync(string tokenHash, string userId, DateTimeOffset expiresAt, CancellationToken ct = default);
    Task<RefreshTokenInfo?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task RevokeAsync(string tokenHash, CancellationToken ct = default);
    Task RevokeAllForUserAsync(string userId, CancellationToken ct = default);
    Task DeleteExpiredAsync(CancellationToken ct = default);
}
