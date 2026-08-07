using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoodPickup.Api.DTOs;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Services;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/staff/auth")]
public sealed class StaffAuthController(
    EmployeeAuthenticationService authenticationService,
    AuthenticationCookieService cookieService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<EmployeeAuthenticationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeAuthenticationResponse>> Login(
        EmployeeLoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(
            request,
            AuthenticationRequestMetadata.FromHttpContext(HttpContext),
            cancellationToken);
        cookieService.SetSessionCookies(
            Response,
            result.RefreshToken.RawToken,
            result.RefreshToken.ExpiresAt);

        return Ok(new EmployeeAuthenticationResponse(
            result.AccessToken.Value,
            result.AccessToken.ExpiresInSeconds,
            result.Employee.MustChangePassword,
            new EmployeeSummary(
                result.Employee.Id,
                result.Employee.FullName,
                result.Employee.Username,
                result.Roles)));
    }

    [Authorize(Policy = AuthenticationConstants.Policies.Employee)]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(
        ChangeEmployeePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                out var employeeId))
        {
            throw new ApiProblemException(
                StatusCodes.Status401Unauthorized,
                "unauthorized",
                "Authentication required");
        }

        await authenticationService.ChangePasswordAsync(
            employeeId,
            request,
            cancellationToken);

        return NoContent();
    }
}
