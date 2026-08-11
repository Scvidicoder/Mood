using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoodPickup.Api.DTOs;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = AuthenticationConstants.Policies.Customer)]
[Route("api/v{version:apiVersion}/profile")]
[Tags("Customer Profile")]
public sealed class ProfileController(
    ICustomerProfileService profileService) : ControllerBase
{
    /// <summary>Returns the authenticated customer's profile and order counts.</summary>
    [HttpGet]
    [ProducesResponseType<CustomerProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CustomerProfileDto>> Get(
        CancellationToken cancellationToken)
    {
        return Ok(await profileService.GetAsync(cancellationToken));
    }

    /// <summary>Updates only the authenticated customer's display name.</summary>
    [HttpPut]
    [ProducesResponseType<CustomerProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerProfileDto>> Update(
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await profileService.UpdateAsync(request, cancellationToken));
    }
}
