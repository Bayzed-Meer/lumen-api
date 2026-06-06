using Lumen.Application.Common.Interfaces;
using Lumen.Infrastructure.Data;
using Lumen.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lumen.Infrastructure.Repositories;

public sealed class RefreshTokenRepository(AppDbContext dbContext) : IRefreshTokenRepository
{
    public Task AddAsync(
        string tokenHash,
        string userId,
        DateTimeOffset expiresAt,
        CancellationToken ct = default)
    {
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt
        });
        return Task.CompletedTask;
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
        RefreshToken? token = await dbContext.RefreshTokens
            .Where(r => r.TokenHash == tokenHash)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (token is not null)
            token.IsRevoked = true;
    }

    public async Task RevokeAllForUserAsync(string userId, CancellationToken ct = default)
    {
        List<RefreshToken> tokens = await dbContext.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (RefreshToken token in tokens)
            token.IsRevoked = true;
    }

    public async Task DeleteExpiredAsync(CancellationToken ct = default)
    {
        await dbContext.RefreshTokens
            .Where(r => r.ExpiresAt < DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
    }
}
