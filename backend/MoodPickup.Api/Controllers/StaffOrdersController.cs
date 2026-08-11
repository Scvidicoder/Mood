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
[Authorize(Policy = AuthenticationConstants.Policies.CanManageOrders)]
[Route("api/v{version:apiVersion}/staff/orders")]
[Tags("Staff Orders")]
public sealed class StaffOrdersController(IStaffOrderService orderService)
    : ControllerBase
{
    /// <summary>Returns newest-first orders with optional status filtering.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<StaffOrderSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<StaffOrderSummaryDto>>> Get(
        [FromQuery] StaffOrderListQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await orderService.GetAsync(query, cancellationToken));
    }

    /// <summary>Returns complete order details for authorized order staff.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<StaffOrderDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StaffOrderDetailDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await orderService.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Confirms a pending order and assigns its estimated ready time.</summary>
    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType<StaffOrderDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StaffOrderDetailDto>> Confirm(
        Guid id,
        ConfirmOrderRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await orderService.ConfirmAsync(id, request, cancellationToken));
    }

    /// <summary>Rejects a pending order with a customer-visible reason.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<StaffOrderDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StaffOrderDetailDto>> Reject(
        Guid id,
        RejectOrderRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await orderService.RejectAsync(id, request, cancellationToken));
    }

    /// <summary>Changes the estimated ready time of a confirmed order.</summary>
    [HttpPut("{id:guid}/estimated-ready-time")]
    [ProducesResponseType<StaffOrderDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StaffOrderDetailDto>> UpdateEstimatedReadyTime(
        Guid id,
        UpdateEstimatedReadyTimeRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await orderService.UpdateEstimatedReadyTimeAsync(
            id,
            request,
            cancellationToken));
    }
}
