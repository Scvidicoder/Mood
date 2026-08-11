using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Admin;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = AuthenticationConstants.Policies.CanManageOptions)]
[Route("api/v{version:apiVersion}/admin/option-groups")]
[Tags("Admin Menu - Option Groups")]
public sealed class AdminOptionGroupsController(IAdminOptionService optionService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AdminOptionGroupDto>>> Get(
        [FromQuery] AdminOptionGroupQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await optionService.GetGroupsAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminOptionGroupDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await optionService.GetGroupAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<AdminOptionGroupDto>> Create(
        CreateOptionGroupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await optionService.CreateGroupAsync(request, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { version = "1.0", id = result.Id },
            result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminOptionGroupDto>> Update(
        Guid id,
        UpdateOptionGroupRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await optionService.UpdateGroupAsync(id, request, cancellationToken));
    }

    [HttpPatch("{id:guid}/active")]
    public async Task<ActionResult<AdminOptionGroupDto>> SetActive(
        Guid id,
        SetActiveRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await optionService.SetGroupActiveAsync(
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
        await optionService.DeleteGroupAsync(
            id,
            request.RowVersion,
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<AdminOptionGroupDto>> Restore(
        Guid id,
        RowVersionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await optionService.RestoreGroupAsync(
            id,
            request,
            cancellationToken));
    }

    [HttpGet("{groupId:guid}/values")]
    public async Task<ActionResult<IReadOnlyList<AdminOptionValueDto>>> GetValues(
        Guid groupId,
        [FromQuery] bool includeDeleted,
        CancellationToken cancellationToken)
    {
        return Ok(await optionService.GetValuesAsync(
            groupId,
            includeDeleted,
            cancellationToken));
    }

    [HttpPost("{groupId:guid}/values")]
    public async Task<ActionResult<AdminOptionValueDto>> CreateValue(
        Guid groupId,
        CreateOptionValueRequest request,
        CancellationToken cancellationToken)
    {
        var result = await optionService.CreateValueAsync(
            groupId,
            request,
            cancellationToken);
        return Created(
            $"/api/v1/admin/option-groups/{groupId}/values",
            result);
    }
}
