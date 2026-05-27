namespace Lumen.Application.DTOs.Auth;

public sealed record LoginResponse
{
    public required string AccessToken { get; init; }
    public required int ExpiresInSeconds { get; init; }
}
