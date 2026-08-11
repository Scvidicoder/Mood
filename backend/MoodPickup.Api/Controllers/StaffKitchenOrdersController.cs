using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = AuthenticationConstants.Policies.Employee)]
[Route("api/v{version:apiVersion}/staff/kitchen")]
[Tags("Kitchen Orders")]
public sealed class StaffKitchenOrdersController(IOrderWorkflowService workflowService)
    : ControllerBase
{
    /// <summary>Returns active kitchen orders with operational filtering.</summary>
    [Authorize(Policy = AuthenticationConstants.Policies.CanViewKitchen)]
    [HttpGet("orders")]
    [ProducesResponseType<PagedResponse<KitchenOrderDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<KitchenOrderDto>>> Get(
        [FromQuery] KitchenOrderListQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await workflowService.GetKitchenOrdersAsync(query, cancellationToken));
    }

    /// <summary>Moves a confirmed order into preparation.</summary>
    [Authorize(Policy = AuthenticationConstants.Policies.CanWorkKitchen)]
    [HttpPost("{id:guid}/start")]
    [ProducesResponseType<KitchenOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KitchenOrderDto>> Start(
        Guid id,
        OrderVersionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await workflowService.StartPreparationAsync(
            id,
            request,
            cancellationToken));
    }

    /// <summary>Moves a preparing order to ready for pickup.</summary>
    [Authorize(Policy = AuthenticationConstants.Policies.CanWorkKitchen)]
    [HttpPost("{id:guid}/ready")]
    [ProducesResponseType<KitchenOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KitchenOrderDto>> Ready(
        Guid id,
        OrderVersionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await workflowService.MarkReadyAsync(
            id,
            request,
            cancellationToken));
    }

    /// <summary>Changes the ETA of a confirmed or preparing order.</summary>
    [Authorize(Policy = AuthenticationConstants.Policies.CanWorkKitchen)]
    [HttpPatch("{id:guid}/eta")]
    [ProducesResponseType<KitchenOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KitchenOrderDto>> UpdateEta(
        Guid id,
        UpdateEstimatedReadyTimeRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await workflowService.UpdateKitchenEtaAsync(
            id,
            request,
            cancellationToken));
    }
}
