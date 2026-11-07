using Ostagar.Platform.Auth;
using Ostagar.Platform.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ostagar.Api.Controllers;

/// <summary>Sign-in and self-introspection.</summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>
    /// Exchanges credentials for an access token bound to one tenant.
    /// </summary>
    /// <remarks>
    /// Allow-listed in <c>anonymous-allowlist.txt</c>. The response carries no stack traces and no
    /// detail distinguishing an unknown account from a wrong password.
    /// </remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await authService.SignInAsync(request, cancellationToken);

        if (!outcome.Succeeded || outcome.Token is null)
        {
            return Unauthorized(new { reason = outcome.Reason.ToString() });
        }

        return Ok(new
        {
            token = outcome.Token.Token,
            expiresAt = outcome.Token.ExpiresAtUtc,
            roles = outcome.Roles ?? [],
        });
    }

    /// <summary>
    /// Echoes what the server resolved from the presented token.
    /// </summary>
    /// <remarks>
    /// Exists because claim resolution is otherwise invisible: a token can authenticate a caller and
    /// still yield no tenant, with a bare 403 as the only symptom. Returning the resolved identity
    /// makes that diagnosable from outside the process, and the client needs it to render the
    /// signed-in state anyway.
    /// </remarks>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Me([FromServices] ICurrentUser currentUser) => Ok(new
    {
        isAuthenticated = currentUser.IsAuthenticated,
        userId = currentUser.UserId,
        tenantId = currentUser.TenantId.IsEmpty ? null : currentUser.TenantId.ToString(),
        displayName = currentUser.DisplayName,
        userName = currentUser.UserName,
    });
}
