using Lumen.Application.Common.Settings;
using Lumen.Application.DTOs.Auth;
using Lumen.Application.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Lumen.API.Controllers;

/// <summary>
/// Handles user authentication, token refresh, and logout.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    IOptions<JwtSettings> jwtSettings) : ControllerBase
{
    private const string RefreshTokenCookie = "refreshToken";

    private readonly CookieOptions _refreshCookieOptions = new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        MaxAge = TimeSpan.FromDays(jwtSettings.Value.RefreshTokenExpiryDays)
    };

    /// <summary>
    /// Authenticates a user and returns an access token. A refresh token is set as an HttpOnly cookie.
    /// </summary>
    /// <param name="request">Login credentials (email or institutional ID + password).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Access token and expiry.</returns>
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        (LoginResponse response, string rawRefreshToken) = await authService.LoginAsync(request, ct);
        Response.Cookies.Append(RefreshTokenCookie, rawRefreshToken, _refreshCookieOptions);
        return Ok(response);
    }

    /// <summary>
    /// Issues a new access token using the refresh token cookie. Rotates the refresh token.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>New access token and expiry.</returns>
    [HttpPost("refresh")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Refresh(CancellationToken ct)
    {
        string? rawToken = Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrEmpty(rawToken))
            return Unauthorized();

        (LoginResponse response, string newRawToken) = await authService.RefreshAsync(rawToken, ct);
        Response.Cookies.Append(RefreshTokenCookie, newRawToken, _refreshCookieOptions);
        return Ok(response);
    }

    /// <summary>
    /// Revokes the current refresh token and clears the cookie. The access token expires naturally.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Logout(CancellationToken ct)
    {
        string? rawToken = Request.Cookies[RefreshTokenCookie];
        if (!string.IsNullOrEmpty(rawToken))
            await authService.LogoutAsync(rawToken, ct);

        Response.Cookies.Delete(RefreshTokenCookie, new CookieOptions { Path = "/api/auth" });
        return NoContent();
    }
}
