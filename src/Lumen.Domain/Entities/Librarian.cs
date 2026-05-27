namespace Lumen.Domain.Entities;

public class Librarian
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string UserId { get; init; }
    public required string InstitutionalId { get; init; }
}
