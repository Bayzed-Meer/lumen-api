using Lumen.Domain.Entities;
using Lumen.Domain.Enums;

namespace Lumen.Application.Common.Interfaces;

public interface IOtpRepository
{
    Task AddAsync(OtpRecord otp, CancellationToken ct = default);
    Task<OtpRecord?> GetActiveOtpAsync(string userId, OtpPurpose purpose, CancellationToken ct = default);
    Task<DateTimeOffset?> GetLastIssuedAtAsync(string userId, OtpPurpose purpose, CancellationToken ct = default);
    Task InvalidateAllForUserAsync(string userId, OtpPurpose purpose, CancellationToken ct = default);
    Task UpdateAsync(OtpRecord otp, CancellationToken ct = default);
    Task<OtpRecord?> GetLatestOtpAsync(string userId, OtpPurpose purpose, CancellationToken ct = default);
    Task DeleteOlderThanAsync(DateTimeOffset threshold, CancellationToken ct = default);
}
