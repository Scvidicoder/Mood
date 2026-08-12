using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MoodPickup.Api.DTOs.Payments;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = AuthenticationConstants.Policies.Customer)]
[Route("api/v{version:apiVersion}/payments")]
[Tags("Payments")]
public sealed class PaymentsController(IPaymentService paymentService) : ControllerBase
{
    /// <summary>Returns business-safe payment state owned by the customer.</summary>
    [HttpGet("{paymentId:guid}")]
    [ProducesResponseType<CustomerPaymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerPaymentDto>> Get(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        return Ok(await paymentService.GetOwnedAsync(paymentId, cancellationToken));
    }

    /// <summary>Verifies an owned payment with Alif on the server.</summary>
    [HttpPost("{paymentId:guid}/verify")]
    [EnableRateLimiting("payment-verification")]
    [ProducesResponseType<CustomerPaymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CustomerPaymentDto>> Verify(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        return Ok(await paymentService.VerifyOwnedAsync(paymentId, cancellationToken));
    }

    /// <summary>Processes an authenticated Alif server-to-server callback.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("payment-callback")]
    [RequestSizeLimit(32 * 1024)]
    [HttpPost("alif/callback")]
    [ProducesResponseType<PaymentCallbackResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentCallbackResult>> AlifCallback(
        AlifCallbackRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await paymentService.HandleAlifCallbackAsync(
            request,
            cancellationToken));
    }
}
