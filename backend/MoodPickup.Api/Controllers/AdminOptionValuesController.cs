using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoodPickup.Api.DTOs.Menu.Admin;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = AuthenticationConstants.Policies.CanManageOptions)]
[Route("api/v{version:apiVersion}/admin/option-values")]
[Tags("Admin Menu - Option Values")]
public sealed class AdminOptionValuesController(IAdminOptionService optionService)
    : ControllerBase
{
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminOptionValueDto>> Update(
        Guid id,
        UpdateOptionValueRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await optionService.UpdateValueAsync(id, request, cancellationToken));
    }

    [HttpPatch("{id:guid}/active")]
    public async Task<ActionResult<AdminOptionValueDto>> SetActive(
        Guid id,
        SetActiveRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await optionService.SetValueActiveAsync(
            id,
            request,
            cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] RowVersionRequest request,
        CancellationToken cancellationToken)
    {
        await optionService.DeleteValueAsync(
            id,
            request.RowVersion,
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<AdminOptionValueDto>> Restore(
        Guid id,
        RowVersionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await optionService.RestoreValueAsync(
            id,
            request,
            cancellationToken));
    }
}
