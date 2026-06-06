using Lumen.Application.Common.Interfaces;
using Lumen.Domain.Entities;
using Lumen.Domain.Enums;
using Lumen.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumen.Infrastructure.Repositories;

public sealed class OtpRepository(AppDbContext dbContext) : IOtpRepository
{
    public Task AddAsync(OtpRecord otp, CancellationToken ct = default)
    {
        dbContext.OtpRecords.Add(otp);
        return Task.CompletedTask;
    }

    public async Task<OtpRecord?> GetActiveOtpAsync(string userId, OtpPurpose purpose, CancellationToken ct = default)
    {
        return await dbContext.OtpRecords
            .AsNoTracking()
            .Where(o => o.UserId == userId
                && o.Purpose == purpose
                && !o.IsInvalidated
                && (o.ExpiresAt > DateTimeOffset.UtcNow
                    || (o.LockedUntil.HasValue && o.LockedUntil.Value > DateTimeOffset.UtcNow)))
            .OrderByDescending(o => o.IssuedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<DateTimeOffset?> GetLastIssuedAtAsync(string userId, OtpPurpose purpose, CancellationToken ct = default)
    {
        return await dbContext.OtpRecords
            .Where(o => o.UserId == userId && o.Purpose == purpose)
            .OrderByDescending(o => o.IssuedAt)
            .Select(o => (DateTimeOffset?)o.IssuedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task InvalidateAllForUserAsync(string userId, OtpPurpose purpose, CancellationToken ct = default)
    {
        List<OtpRecord> records = await dbContext.OtpRecords
            .Where(o => o.UserId == userId && o.Purpose == purpose && !o.IsInvalidated)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (OtpRecord record in records)
            record.Invalidate();
    }

    public Task UpdateAsync(OtpRecord otp, CancellationToken ct = default)
    {
        dbContext.OtpRecords.Update(otp);
        return Task.CompletedTask;
    }

    public async Task<OtpRecord?> GetLatestOtpAsync(string userId, OtpPurpose purpose, CancellationToken ct = default)
    {
        return await dbContext.OtpRecords
            .AsNoTracking()
            .Where(o => o.UserId == userId && o.Purpose == purpose)
            .OrderByDescending(o => o.IssuedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task DeleteOlderThanAsync(DateTimeOffset threshold, CancellationToken ct = default)
    {
        await dbContext.OtpRecords
            .Where(o => o.IssuedAt < threshold)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
    }
}
