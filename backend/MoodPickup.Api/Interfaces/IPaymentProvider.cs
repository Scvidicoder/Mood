using MoodPickup.Api.DTOs.Payments;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Interfaces;

public interface IPaymentProvider
{
    PaymentProvider Provider { get; }

    Task<PaymentLaunchResponse> CreatePaymentLaunchAsync(
        PaymentProviderLaunchRequest request,
        CancellationToken cancellationToken);

    Task<PaymentProviderStatusResult> CheckPaymentStatusAsync(
        string providerOrderId,
        CancellationToken cancellationToken);

    Task<PaymentProviderRefundResult> RefundAsync(
        PaymentProviderRefundRequest request,
        CancellationToken cancellationToken);
}

public sealed record PaymentProviderLaunchRequest(
    Guid PaymentId,
    string ProviderOrderId,
    decimal Amount,
    string Currency,
    string CustomerPhoneNumber,
    string Description);

public sealed record PaymentProviderStatusResult(
    string ProviderOrderId,
    string TransactionId,
    string ProviderStatus,
    decimal Amount,
    PaymentStatus Status,
    string? FailureReason);

public sealed record PaymentProviderRefundRequest(
    string ProviderOrderId,
    string ProviderTransactionId,
    decimal Amount,
    string Currency);

public sealed record PaymentProviderRefundResult(
    bool Accepted,
    string ProviderStatus,
    string? ProviderReference);
