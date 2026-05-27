namespace Lumen.Application.DTOs.Auth;

public sealed record CreateAccountResponse
{
    public required string UserId { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required bool IsVerified { get; init; }
}
