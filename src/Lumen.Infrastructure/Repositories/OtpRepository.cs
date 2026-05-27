using Lumen.Application.Common.Interfaces;
using Lumen.Domain.Entities;
using Lumen.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumen.Infrastructure.Repositories;

public sealed class OtpRepository(AppDbContext dbContext) : IOtpRepository
{
    public async Task AddAsync(OtpRecord otp, CancellationToken ct = default)
    {
        dbContext.OtpRecords.Add(otp);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<OtpRecord?> GetActiveOtpAsync(string userId, CancellationToken ct = default)
    {
        return await dbContext.OtpRecords
            .AsNoTracking()
            .Where(o => o.UserId == userId
                && !o.IsInvalidated
                && (o.ExpiresAt > DateTimeOffset.UtcNow
                    || (o.LockedUntil.HasValue && o.LockedUntil.Value > DateTimeOffset.UtcNow)))
            .OrderByDescending(o => o.IssuedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<DateTimeOffset?> GetLastIssuedAtAsync(string userId, CancellationToken ct = default)
    {
        return await dbContext.OtpRecords
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.IssuedAt)
            .Select(o => (DateTimeOffset?)o.IssuedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task InvalidateAllForUserAsync(string userId, CancellationToken ct = default)
    {
        await dbContext.OtpRecords
            .Where(o => o.UserId == userId && !o.IsInvalidated)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsInvalidated, true), ct)
            .ConfigureAwait(false);
    }

    public async Task UpdateAsync(OtpRecord otp, CancellationToken ct = default)
    {
        await dbContext.OtpRecords
            .Where(o => o.Id == otp.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.FailedAttempts, otp.FailedAttempts)
                .SetProperty(o => o.IsInvalidated, otp.IsInvalidated)
                .SetProperty(o => o.LockedUntil, otp.LockedUntil), ct)
            .ConfigureAwait(false);
    }
}
