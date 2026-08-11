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
[Authorize(Policy = AuthenticationConstants.Policies.CanWorkKitchen)]
[Route("api/v{version:apiVersion}/staff/kitchen/orders")]
[Tags("Kitchen Orders")]
public sealed class StaffKitchenOrdersController(IStaffOrderService orderService)
    : ControllerBase
{
    /// <summary>Returns confirmed orders for future kitchen workflow consumers.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<StaffOrderSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<StaffOrderSummaryDto>>> Get(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await orderService.GetKitchenOrdersAsync(query, cancellationToken));
    }
}
