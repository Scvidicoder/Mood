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
[Authorize(Policy = AuthenticationConstants.Policies.CanManageCategories)]
[Route("api/v{version:apiVersion}/admin/categories")]
[Tags("Admin Menu - Categories")]
public sealed class AdminCategoriesController(IAdminCategoryService categoryService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AdminCategoryDto>>> Get(
        [FromQuery] AdminCategoryQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await categoryService.GetCategoriesAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminCategoryDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await categoryService.GetCategoryAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<AdminCategoryDto>> Create(
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await categoryService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { version = "1.0", id = result.Id },
            result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminCategoryDto>> Update(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await categoryService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPut("reorder")]
    public async Task<ActionResult<IReadOnlyList<AdminCategoryDto>>> Reorder(
        ReorderCategoriesRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await categoryService.ReorderAsync(request, cancellationToken));
    }

    [HttpPatch("{id:guid}/visibility")]
    public async Task<ActionResult<AdminCategoryDto>> SetVisibility(
        Guid id,
        SetVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await categoryService.SetVisibilityAsync(
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
        await categoryService.DeleteAsync(id, request.RowVersion, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<AdminCategoryDto>> Restore(
        Guid id,
        RowVersionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await categoryService.RestoreAsync(id, request, cancellationToken));
    }
}
