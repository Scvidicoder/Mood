using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = AuthenticationConstants.Policies.CanCompleteOrders)]
[Route("api/v{version:apiVersion}/staff/orders")]
[Tags("Staff Order Completion")]
public sealed class StaffOrderCompletionController(IOrderWorkflowService workflowService)
    : ControllerBase
{
    /// <summary>Records a cash or card payment for a ready pay-on-pickup order.</summary>
    [HttpPost("{id:guid}/record-payment")]
    [ProducesResponseType<StaffOrderDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StaffOrderDetailDto>> RecordPayment(
        Guid id,
        RecordPaymentRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await workflowService.RecordPaymentAsync(
            id,
            request,
            cancellationToken));
    }

    /// <summary>Completes a ready order after required pickup payment.</summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType<StaffOrderDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StaffOrderDetailDto>> Complete(
        Guid id,
        OrderVersionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await workflowService.CompleteAsync(
            id,
            request,
            cancellationToken));
    }
}
