using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoodPickup.Api.DTOs.Payments;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[ApiExplorerSettings(IgnoreApi = true)]
[Authorize(Policy = AuthenticationConstants.Policies.Customer)]
[DevelopmentOnly]
[Route("api/v{version:apiVersion}/dev/payments")]
public sealed class DevelopmentPaymentsController(IPaymentService paymentService)
    : ControllerBase
{
    [HttpPost("{paymentId:guid}/success")]
    public Task<CustomerPaymentDto> Success(
        Guid paymentId,
        CancellationToken cancellationToken) =>
        paymentService.SimulateDevelopmentStatusAsync(
            paymentId,
            PaymentStatus.Paid,
            cancellationToken);

    [HttpPost("{paymentId:guid}/failed")]
    public Task<CustomerPaymentDto> Failed(
        Guid paymentId,
        CancellationToken cancellationToken) =>
        paymentService.SimulateDevelopmentStatusAsync(
            paymentId,
            PaymentStatus.Failed,
            cancellationToken);

    [HttpPost("{paymentId:guid}/cancel")]
    public Task<CustomerPaymentDto> Cancel(
        Guid paymentId,
        CancellationToken cancellationToken) =>
        paymentService.SimulateDevelopmentStatusAsync(
            paymentId,
            PaymentStatus.Cancelled,
            cancellationToken);

    [HttpPost("{paymentId:guid}/pending")]
    public Task<CustomerPaymentDto> Pending(
        Guid paymentId,
        CancellationToken cancellationToken) =>
        paymentService.SimulateDevelopmentStatusAsync(
            paymentId,
            PaymentStatus.Pending,
            cancellationToken);
}
