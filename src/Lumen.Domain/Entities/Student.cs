namespace Lumen.Domain.Entities;

public class Student
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string UserId { get; init; }
    public required string InstitutionalId { get; init; }
}
