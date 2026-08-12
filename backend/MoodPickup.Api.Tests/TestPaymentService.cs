using MoodPickup.Api.DTOs.Payments;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Tests;

internal sealed class TestPaymentService : IPaymentService
{
    public bool MarkRefundRequired { get; init; }

    public int MarkRefundRequiredCalls { get; private set; }

    public Task<PaymentLaunchResponse> CreateForOrderAsync(
        Order order,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This test does not create an online payment.");

    public Task<CustomerPaymentDto> GetOwnedAsync(
        Guid paymentId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<CustomerPaymentDto> VerifyOwnedAsync(
        Guid paymentId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<PaymentCallbackResult> HandleAlifCallbackAsync(
        AlifCallbackRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<CustomerPaymentDto> SimulateDevelopmentStatusAsync(
        Guid paymentId,
        PaymentStatus status,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> MarkRefundRequiredForRejectedOrderAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        MarkRefundRequiredCalls++;
        if (!MarkRefundRequired || order.Payment?.Status != PaymentStatus.Paid)
        {
            return Task.FromResult(false);
        }

        order.Payment.Status = PaymentStatus.RefundRequired;
        return Task.FromResult(true);
    }
}
