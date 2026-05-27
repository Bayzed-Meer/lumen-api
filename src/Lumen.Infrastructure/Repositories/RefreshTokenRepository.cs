using Lumen.Application.Common.Interfaces;
using Lumen.Infrastructure.Data;
using Lumen.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lumen.Infrastructure.Repositories;

public sealed class RefreshTokenRepository(AppDbContext dbContext) : IRefreshTokenRepository
{
    public async Task AddAsync(
        string tokenHash,
        string userId,
        DateTimeOffset expiresAt,
        CancellationToken ct = default)
    {
        RefreshToken token = new()
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt
        };

        dbContext.RefreshTokens.Add(token);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<RefreshTokenInfo?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        return await dbContext.RefreshTokens
            .AsNoTracking()
            .Where(r => r.TokenHash == tokenHash)
            .Select(r => new RefreshTokenInfo(r.Id, r.UserId, r.IsRevoked, r.ExpiresAt))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task RevokeAsync(string tokenHash, CancellationToken ct = default)
    {
        await dbContext.RefreshTokens
            .Where(r => r.TokenHash == tokenHash)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true), ct)
            .ConfigureAwait(false);
    }

    public async Task RevokeAllForUserAsync(string userId, CancellationToken ct = default)
    {
        await dbContext.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true), ct)
            .ConfigureAwait(false);
    }

    public async Task DeleteExpiredAsync(CancellationToken ct = default)
    {
        await dbContext.RefreshTokens
            .Where(r => r.ExpiresAt < DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
    }
}
