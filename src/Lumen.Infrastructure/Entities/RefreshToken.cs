namespace Lumen.Infrastructure.Entities;

public class RefreshToken
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string UserId { get; init; }
    public required string TokenHash { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
