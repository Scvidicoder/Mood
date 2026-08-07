using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Public;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[AllowAnonymous]
[Route("api/v{version:apiVersion}")]
[Tags("Public Menu")]
public sealed class PublicMenuController(IPublicMenuService menuService)
    : ControllerBase
{
    /// <summary>Returns categories that currently contain public products.</summary>
    [HttpGet("categories")]
    [ProducesResponseType<IReadOnlyList<PublicCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublicCategoryDto>>> GetCategories(
        CancellationToken cancellationToken)
    {
        return Ok(await menuService.GetCategoriesAsync(cancellationToken));
    }

    /// <summary>Returns the paged, searchable public product catalog.</summary>
    [HttpGet("products")]
    [ProducesResponseType<PagedResponse<PublicProductListItemDto>>(
        StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PublicProductListItemDto>>> GetProducts(
        [FromQuery] PublicProductQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await menuService.GetProductsAsync(query, cancellationToken));
    }

    /// <summary>Returns the selectable public configuration for one product.</summary>
    [HttpGet("products/{id:guid}")]
    [ProducesResponseType<PublicProductDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicProductDetailDto>> GetProduct(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await menuService.GetProductAsync(id, cancellationToken));
    }
}
