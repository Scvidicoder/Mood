using Microsoft.Extensions.Options;
using MoodPickup.Api.DTOs.Payments;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Services;

public sealed class AlifPaymentProvider(
    AlifSignatureService signatureService,
    AlifPaymentClient paymentClient,
    IOptionsMonitor<AlifOptions> options) : IPaymentProvider
{
    public PaymentProvider Provider => PaymentProvider.Alif;

    public Task<PaymentLaunchResponse> CreatePaymentLaunchAsync(
        PaymentProviderLaunchRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = options.CurrentValue;
        if (!current.Enabled)
        {
            throw PaymentProviderUnavailable();
        }

        if (!string.Equals(request.Currency, "TJS", StringComparison.Ordinal))
        {
            throw new ApiProblemException(
                StatusCodes.Status409Conflict,
                "payment_status_conflict",
                "Alif WebCheckout requires TJS payments",
                "PAYMENT_STATUS_CONFLICT");
        }

        var amount = AlifSignatureService.FormatAmount(request.Amount);
        var returnUrl = AppendPaymentId(current.ReturnUrl, request.PaymentId);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["key"] = current.Key,
            ["token"] = signatureService.CreatePaymentToken(
                request.ProviderOrderId,
                request.Amount,
                current.CallbackUrl),
            ["orderId"] = request.ProviderOrderId,
            ["gate"] = current.Gate,
            ["amount"] = amount,
            ["callbackUrl"] = current.CallbackUrl,
            ["returnUrl"] = returnUrl,
            ["phone"] = ToNationalPhone(request.CustomerPhoneNumber),
            ["info"] = request.Description
        };

        return Task.FromResult(new PaymentLaunchResponse(
            request.PaymentId,
            current.BaseUrl,
            HttpMethod.Post.Method,
            fields));
    }

    public async Task<PaymentProviderStatusResult> CheckPaymentStatusAsync(
        string providerOrderId,
        CancellationToken cancellationToken)
    {
        var response = await paymentClient.CheckStatusAsync(
            providerOrderId,
            cancellationToken);
        _ = AlifStatusMapper.TryMap(
            response.Status,
            out var status,
            out var failureReason);
        return new PaymentProviderStatusResult(
            response.OrderId,
            response.TransactionId,
            response.Status,
            response.Amount,
            status,
            failureReason);
    }

    public Task<PaymentProviderRefundResult> RefundAsync(
        PaymentProviderRefundRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new ApiProblemException(
            StatusCodes.Status501NotImplemented,
            "payment_refund_protocol_unavailable",
            "The Alif refund protocol is awaiting official confirmation",
            "PAYMENT_REFUND_PROTOCOL_UNAVAILABLE",
            "No refund request was sent and the payment was not marked refunded.");
    }

    private static string AppendPaymentId(string returnUrl, Guid paymentId)
    {
        var separator = returnUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{returnUrl}{separator}paymentId={paymentId:D}";
    }

    private static string ToNationalPhone(string phoneNumber)
    {
        var normalized = phoneNumber.Trim();
        return normalized.StartsWith("+992", StringComparison.Ordinal) &&
               normalized.Length == 13
            ? normalized[4..]
            : normalized.TrimStart('+');
    }

    private static ApiProblemException PaymentProviderUnavailable()
    {
        return new ApiProblemException(
            StatusCodes.Status503ServiceUnavailable,
            "payment_provider_unavailable",
            "The payment provider is unavailable",
            "PAYMENT_PROVIDER_UNAVAILABLE");
    }
}
