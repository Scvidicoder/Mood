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
[Authorize(Policy = AuthenticationConstants.Policies.CanManageProducts)]
[Route("api/v{version:apiVersion}/admin/products")]
[Tags("Admin Menu - Products")]
public sealed class AdminProductsController(IAdminProductService productService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AdminProductListItemDto>>> Get(
        [FromQuery] AdminProductQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await productService.GetProductsAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminProductDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await productService.GetProductAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<MenuMutationResponse<AdminProductDto>>> Create(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await productService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { version = "1.0", id = result.Resource.Id },
            result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MenuMutationResponse<AdminProductDto>>> Update(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await productService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<MenuMutationResponse<AdminProductDto>>> Duplicate(
        Guid id,
        DuplicateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await productService.DuplicateAsync(
            id,
            request,
            cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { version = "1.0", id = result.Resource.Id },
            result);
    }

    [HttpPut("reorder")]
    public async Task<ActionResult<IReadOnlyList<AdminProductListItemDto>>> Reorder(
        ReorderProductsRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await productService.ReorderAsync(request, cancellationToken));
    }

    [HttpPatch("{id:guid}/availability")]
    public async Task<ActionResult<MenuMutationResponse<AdminProductDto>>>
        SetAvailability(
            Guid id,
            SetAvailabilityRequest request,
            CancellationToken cancellationToken)
    {
        return Ok(await productService.SetAvailabilityAsync(
            id,
            request,
            cancellationToken));
    }

    [HttpPatch("{id:guid}/visibility")]
    public async Task<ActionResult<MenuMutationResponse<AdminProductDto>>> SetVisibility(
        Guid id,
        SetVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await productService.SetVisibilityAsync(
            id,
            request,
            cancellationToken));
    }

    [HttpPut("{id:guid}/image")]
    public async Task<ActionResult<MenuMutationResponse<AdminProductDto>>> AssignImage(
        Guid id,
        AssignProductImageRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await productService.AssignImageAsync(
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
        await productService.DeleteAsync(id, request.RowVersion, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<MenuMutationResponse<AdminProductDto>>> Restore(
        Guid id,
        RowVersionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await productService.RestoreAsync(id, request, cancellationToken));
    }
}
