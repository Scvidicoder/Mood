using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MoodPickup.Api.DTOs;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system")]
public sealed class SystemController(
    IHostEnvironment hostEnvironment,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Returns non-sensitive service and runtime information.</summary>
    [HttpGet("info")]
    [ProducesResponseType<SystemInfoResponse>(StatusCodes.Status200OK)]
    public ActionResult<SystemInfoResponse> GetInfo()
    {
        return Ok(new SystemInfoResponse(
            Service: "MoodPickup.Api",
            Environment: hostEnvironment.EnvironmentName,
            ApiVersion: "1.0",
            UtcTime: timeProvider.GetUtcNow()));
    }
}
