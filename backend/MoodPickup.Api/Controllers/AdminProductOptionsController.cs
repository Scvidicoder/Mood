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
[Authorize(Policy = AuthenticationConstants.Policies.CanManageMenu)]
[Route("api/v{version:apiVersion}/admin/products")]
[Tags("Admin Menu - Product Configuration")]
public sealed class AdminProductOptionsController(
    IAdminProductConfigurationService configurationService) : ControllerBase
{
    [HttpPost("{productId:guid}/option-groups")]
    public async Task<ActionResult<MenuMutationResponse<AdminProductOptionGroupDto>>>
        AddGroup(
            Guid productId,
            CreateProductOptionGroupRequest request,
            CancellationToken cancellationToken)
    {
        var result = await configurationService.AddGroupAsync(
            productId,
            request,
            cancellationToken);
        return Created(
            $"/api/v1/admin/products/{productId}/option-groups/{result.Resource.Id}",
            result);
    }

    [HttpPut("{productId:guid}/option-groups/{assignmentId:guid}")]
    public async Task<ActionResult<MenuMutationResponse<AdminProductOptionGroupDto>>>
        UpdateGroup(
            Guid productId,
            Guid assignmentId,
            UpdateProductOptionGroupRequest request,
            CancellationToken cancellationToken)
    {
        return Ok(await configurationService.UpdateGroupAsync(
            productId,
            assignmentId,
            request,
            cancellationToken));
    }

    [HttpDelete("{productId:guid}/option-groups/{assignmentId:guid}")]
    public async Task<IActionResult> DeleteGroup(
        Guid productId,
        Guid assignmentId,
        [FromQuery] RowVersionRequest request,
        CancellationToken cancellationToken)
    {
        await configurationService.DeleteGroupAsync(
            productId,
            assignmentId,
            request.RowVersion,
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{productId:guid}/option-groups/{assignmentId:guid}/restore")]
    public async Task<ActionResult<MenuMutationResponse<AdminProductOptionGroupDto>>>
        RestoreGroup(
            Guid productId,
            Guid assignmentId,
            RowVersionRequest request,
            CancellationToken cancellationToken)
    {
        return Ok(await configurationService.RestoreGroupAsync(
            productId,
            assignmentId,
            request,
            cancellationToken));
    }

    [HttpPost("{productId:guid}/option-groups/{assignmentId:guid}/values")]
    public async Task<ActionResult<MenuMutationResponse<AdminProductOptionValueDto>>>
        AddValue(
            Guid productId,
            Guid assignmentId,
            CreateProductOptionValueRequest request,
            CancellationToken cancellationToken)
    {
        var result = await configurationService.AddValueAsync(
            productId,
            assignmentId,
            request,
            cancellationToken);
        return Created(
            $"/api/v1/admin/products/{productId}/option-values/{result.Resource.Id}",
            result);
    }

    [HttpPut("{productId:guid}/option-values/{assignmentValueId:guid}")]
    public async Task<ActionResult<MenuMutationResponse<AdminProductOptionValueDto>>>
        UpdateValue(
            Guid productId,
            Guid assignmentValueId,
            UpdateProductOptionValueRequest request,
            CancellationToken cancellationToken)
    {
        return Ok(await configurationService.UpdateValueAsync(
            productId,
            assignmentValueId,
            request,
            cancellationToken));
    }

    [HttpDelete("{productId:guid}/option-values/{assignmentValueId:guid}")]
    public async Task<IActionResult> DeleteValue(
        Guid productId,
        Guid assignmentValueId,
        [FromQuery] RowVersionRequest request,
        CancellationToken cancellationToken)
    {
        await configurationService.DeleteValueAsync(
            productId,
            assignmentValueId,
            request.RowVersion,
            cancellationToken);
        return NoContent();
    }
}
