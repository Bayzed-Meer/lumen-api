using Lumen.Domain.Entities;

namespace Lumen.Application.Common.Interfaces;

public interface IOtpRepository
{
    Task AddAsync(OtpRecord otp, CancellationToken ct = default);
    Task<OtpRecord?> GetActiveOtpAsync(string userId, CancellationToken ct = default);
    Task<DateTimeOffset?> GetLastIssuedAtAsync(string userId, CancellationToken ct = default);
    Task InvalidateAllForUserAsync(string userId, CancellationToken ct = default);
    Task UpdateAsync(OtpRecord otp, CancellationToken ct = default);
}
