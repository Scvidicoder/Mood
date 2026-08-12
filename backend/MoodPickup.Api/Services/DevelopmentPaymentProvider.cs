using MoodPickup.Api.DTOs.Payments;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class DevelopmentPaymentProvider(IHostEnvironment environment)
    : IPaymentProvider
{
    public PaymentProvider Provider => PaymentProvider.Development;

    public Task<PaymentLaunchResponse> CreatePaymentLaunchAsync(
        PaymentProviderLaunchRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDevelopment();
        return Task.FromResult(new PaymentLaunchResponse(
            request.PaymentId,
            $"/dev/payment/{request.PaymentId:D}",
            HttpMethod.Get.Method,
            new Dictionary<string, string>()));
    }

    public Task<PaymentProviderStatusResult> CheckPaymentStatusAsync(
        string providerOrderId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDevelopment();
        throw new InvalidOperationException(
            "Development payment status is changed only through the simulator.");
    }

    public Task<PaymentProviderRefundResult> RefundAsync(
        PaymentProviderRefundRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDevelopment();
        throw new InvalidOperationException(
            "The Development payment simulator does not implement refunds.");
    }

    private void EnsureDevelopment()
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "The Development payment provider is available only in Development.");
        }
    }
}
