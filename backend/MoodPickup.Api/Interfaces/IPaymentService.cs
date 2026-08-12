using MoodPickup.Api.DTOs.Payments;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Interfaces;

public interface IPaymentService
{
    Task<PaymentLaunchResponse> CreateForOrderAsync(
        Order order,
        CancellationToken cancellationToken);

    Task<CustomerPaymentDto> GetOwnedAsync(
        Guid paymentId,
        CancellationToken cancellationToken);

    Task<CustomerPaymentDto> VerifyOwnedAsync(
        Guid paymentId,
        CancellationToken cancellationToken);

    Task<PaymentCallbackResult> HandleAlifCallbackAsync(
        AlifCallbackRequest request,
        CancellationToken cancellationToken);

    Task<CustomerPaymentDto> SimulateDevelopmentStatusAsync(
        Guid paymentId,
        PaymentStatus status,
        CancellationToken cancellationToken);

    Task<bool> MarkRefundRequiredForRejectedOrderAsync(
        Order order,
        CancellationToken cancellationToken);
}
