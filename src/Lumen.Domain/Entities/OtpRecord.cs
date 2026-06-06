using Lumen.Domain.Enums;

namespace Lumen.Domain.Entities;

public class OtpRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string UserId { get; init; }
    public required string CodeHash { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public bool IsInvalidated { get; private set; }
    public int FailedAttempts { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public OtpPurpose Purpose { get; init; } = OtpPurpose.Registration;
    public bool IsUsed { get; private set; }

    public static OtpRecord Create(string userId, string codeHash, DateTimeOffset issuedAt, DateTimeOffset expiresAt, OtpPurpose purpose = OtpPurpose.Registration)
    {
        if (string.IsNullOrWhiteSpace(codeHash))
            throw new ArgumentException("Code hash cannot be empty.", nameof(codeHash));
        if (expiresAt <= issuedAt)
            throw new ArgumentException("ExpiresAt must be after IssuedAt.", nameof(expiresAt));

        return new OtpRecord
        {
            UserId = userId,
            CodeHash = codeHash,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
            Purpose = purpose
        };
    }

    // Returns remaining attempts after this failure; invalidates and sets LockedUntil atomically when threshold is reached.
    public int RecordFailedAttempt(int maxAttempts, DateTimeOffset lockedUntil)
    {
        FailedAttempts++;
        if (FailedAttempts >= maxAttempts)
        {
            IsInvalidated = true;
            LockedUntil = lockedUntil;
        }
        return maxAttempts - FailedAttempts;
    }

    public void Invalidate() => IsInvalidated = true;

    public void Consume()
    {
        IsUsed = true;
        IsInvalidated = true;
    }
}
