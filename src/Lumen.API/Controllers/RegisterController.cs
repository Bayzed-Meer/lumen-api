using Lumen.Application.Common.Constants;
using Lumen.Application.DTOs.Auth;
using Lumen.Application.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lumen.API.Controllers;

/// <summary>
/// Handles account creation and verification for library users.
/// </summary>
[ApiController]
[Route("api/register")]
public sealed class RegisterController(IRegisterService registerService) : ControllerBase
{
    /// <summary>
    /// Creates a new user account (Student, Faculty, or Librarian).
    /// Admins may create any role; Librarians may only create Student or Faculty accounts.
    /// </summary>
    /// <param name="request">Account details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created account summary.</returns>
    [HttpPost]
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
        string currentUserRole = User.FindFirst(ClaimNames.Role)?.Value ?? string.Empty;
        CreateAccountResponse response = await registerService.CreateAccountAsync(request, currentUserRole, ct);
        return Created(string.Empty, response);
    }

    /// <summary>
    /// Verifies a new account using the OTP sent to the registered email.
    /// </summary>
    /// <param name="request">The identity and OTP code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success message.</returns>
    [HttpPost("verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        CancellationToken ct)
    {
        await registerService.VerifyOtpAsync(request, ct);
        return Ok(new { message = "Account verified successfully." });
    }

    /// <summary>
    /// Resends an OTP to the registered email for an unverified account.
    /// </summary>
    /// <param name="request">The user identity (email or institutional ID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success message.</returns>
    [HttpPost("resend-otp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ResendOtp(
        [FromBody] ResendOtpRequest request,
        CancellationToken ct)
    {
        await registerService.ResendOtpAsync(request, ct);
        return Ok(new { message = "A new OTP has been sent to the registered email address." });
    }
}
