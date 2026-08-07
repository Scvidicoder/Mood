using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MoodPickup.Api.DTOs;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Services;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth")]
public sealed class SessionController(
    SessionService sessionService,
    AuthenticationCookieService cookieService) : ControllerBase
{
    [HttpPost("refresh")]
    [ServiceFilter<DoubleSubmitCsrfFilter>]
    [ProducesResponseType<RefreshSessionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RefreshSessionResponse>> Refresh(
        CancellationToken cancellationToken)
    {
        var refreshToken = cookieService.GetRefreshToken(Request);

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ApiProblemException(
                StatusCodes.Status401Unauthorized,
                "invalid_refresh_token",
                "The session is invalid or expired",
                "INVALID_REFRESH_TOKEN");
        }

        var result = await sessionService.RefreshAsync(
            refreshToken,
            AuthenticationRequestMetadata.FromHttpContext(HttpContext),
            cancellationToken);
        cookieService.SetSessionCookies(
            Response,
            result.RefreshToken,
            result.RefreshTokenExpiresAt);

        return Ok(new RefreshSessionResponse(
            result.AccessToken.Value,
            result.AccessToken.ExpiresInSeconds));
    }

    [HttpPost("logout")]
    [ServiceFilter<DoubleSubmitCsrfFilter>]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sessionService.LogoutAsync(
            cookieService.GetRefreshToken(Request),
            AuthenticationRequestMetadata.FromHttpContext(HttpContext),
            cancellationToken);
        cookieService.ClearSessionCookies(Response);

        return NoContent();
    }
}
