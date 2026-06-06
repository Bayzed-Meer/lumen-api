using System.Security.Claims;
using Lumen.Application.Common.Constants;
using Lumen.Application.Common.Settings;
using Lumen.Application.DTOs.Auth;
using Lumen.Application.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Lumen.API.Controllers;

/// <summary>
/// Handles user registration, authentication, token refresh, logout, and password management.
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
    /// Creates a new user account (Student, Faculty, or Librarian).
    /// Admins may create Student, Faculty, or Librarian accounts; Librarians may only create Student or Faculty accounts.
    /// </summary>
    /// <param name="request">Account details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created account summary.</returns>
    [HttpPost("register")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Librarian}")]
    [ProducesResponseType<CreateAccountResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateAccountResponse>> CreateAccount(
        [FromBody] CreateAccountRequest request,
        CancellationToken ct)
    {
        string currentUserRole = User.FindFirstValue(ClaimNames.Role) ?? string.Empty;
        CreateAccountResponse response = await authService.CreateAccountAsync(request, currentUserRole, ct);
        return Created(string.Empty, response);
    }

    /// <summary>
    /// Verifies a new account using the OTP sent to the registered email.
    /// </summary>
    /// <param name="request">The identity and OTP code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success message.</returns>
    [HttpPost("register/verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        CancellationToken ct)
    {
        await authService.VerifyOtpAsync(request, ct);
        return Ok(new { message = "Account verified successfully." });
    }

    /// <summary>
    /// Resends an OTP to the registered email for an unverified account.
    /// </summary>
    /// <param name="request">The user identity (email or institutional ID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success message.</returns>
    [HttpPost("register/resend-otp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ResendOtp(
        [FromBody] ResendOtpRequest request,
        CancellationToken ct)
    {
        await authService.ResendOtpAsync(request, ct);
        return Ok(new { message = "A new OTP has been sent to the registered email address." });
    }

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

    /// <summary>
    /// Revokes all active sessions for the authenticated user and clears the refresh token cookie.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("logout-all-devices")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> LogoutAllDevices(CancellationToken ct)
    {
        string userId = User.FindFirstValue(ClaimNames.Subject)
            ?? throw new InvalidOperationException("User ID claim missing.");

        await authService.LogoutAllDevicesAsync(userId, ct);
        Response.Cookies.Delete(RefreshTokenCookie, new CookieOptions { Path = "/api/auth" });
        return NoContent();
    }

    /// <summary>
    /// Changes the authenticated user's password. Revokes all existing sessions and returns new tokens.
    /// </summary>
    /// <param name="request">Current and new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>New access token and expiry.</returns>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoginResponse>> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        string userId = User.FindFirstValue(ClaimNames.Subject)
            ?? throw new InvalidOperationException("User ID claim missing.");

        (LoginResponse response, string rawRefreshToken) = await authService.ChangePasswordAsync(userId, request, ct);
        Response.Cookies.Append(RefreshTokenCookie, rawRefreshToken, _refreshCookieOptions);
        return Ok(response);
    }

    /// <summary>
    /// Sends a password reset OTP to the specified email. Always returns 200 to prevent user enumeration.
    /// </summary>
    /// <param name="request">Email address to send the reset code to.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await authService.ForgotPasswordAsync(request, ct);
        return Ok(new { message = "A reset code has been sent." });
    }

    /// <summary>
    /// Resends a password reset OTP. Always returns 200 to prevent user enumeration.
    /// </summary>
    /// <param name="request">Email address to resend the reset code to.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("resend-reset-otp")]
    [EnableRateLimiting("resend-reset-otp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> ResendResetOtp([FromBody] ResendResetOtpRequest request, CancellationToken ct)
    {
        await authService.ResendResetOtpAsync(request, ct);
        return Ok(new { message = "A reset code has been sent." });
    }

    /// <summary>
    /// Resets the user's password using a valid OTP. Returns new tokens on success.
    /// </summary>
    /// <param name="request">Email, OTP, and new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>New access token and expiry.</returns>
    [HttpPost("reset-password")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        (LoginResponse response, string rawRefreshToken) = await authService.ResetPasswordAsync(request, ct);
        Response.Cookies.Append(RefreshTokenCookie, rawRefreshToken, _refreshCookieOptions);
        return Ok(response);
    }
}
