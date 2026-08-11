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
[Authorize(Policy = AuthenticationConstants.Policies.Customer)]
[Route("api/v{version:apiVersion}/orders")]
[Tags("Customer Orders")]
public sealed class OrdersController(IOrderService orderService) : ControllerBase
{
    /// <summary>Creates a customer order from a server-revalidated cart draft.</summary>
    [HttpPost]
    [ProducesResponseType<OrderDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDetailDto>> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await orderService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { version = "1.0", id = order.Id },
            order);
    }

    /// <summary>Returns one order owned by the authenticated customer.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await orderService.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Returns newest-first summaries of the authenticated customer's orders.</summary>
    [HttpGet("mine")]
    [ProducesResponseType<PagedResponse<OrderSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResponse<OrderSummaryDto>>> GetMine(
        [FromQuery] OrderListQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await orderService.GetMineAsync(query, cancellationToken));
    }

    /// <summary>Cancels an owned order that is still awaiting staff confirmation.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<OrderDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDetailDto>> Cancel(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await orderService.CancelAsync(id, cancellationToken));
    }

    /// <summary>Validates an owned historical snapshot against the current menu.</summary>
    [HttpPost("{id:guid}/repeat")]
    [ProducesResponseType<RepeatOrderResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RepeatOrderResultDto>> Repeat(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await orderService.RepeatAsync(id, cancellationToken));
    }
}
