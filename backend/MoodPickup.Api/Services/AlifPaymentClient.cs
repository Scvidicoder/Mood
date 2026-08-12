using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MoodPickup.Api.DTOs.Payments;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Services;

public sealed class AlifPaymentClient(
    HttpClient httpClient,
    AlifSignatureService signatureService,
    IOptionsMonitor<AlifOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    internal async Task<AlifStatusResponse> CheckStatusAsync(
        string providerOrderId,
        CancellationToken cancellationToken)
    {
        var current = options.CurrentValue;
        EnsureEnabled(current);
        httpClient.Timeout = TimeSpan.FromSeconds(current.ApiTimeoutSeconds);
        var endpoint = new Uri(new Uri(current.BaseUrl, UriKind.Absolute), "checktxn");
        var request = new AlifStatusCheckRequest(
            providerOrderId,
            current.Key,
            signatureService.CreateStatusCheckToken(providerOrderId));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync(
                endpoint,
                request,
                JsonOptions,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException &&
            !cancellationToken.IsCancellationRequested)
        {
            throw ProviderUnavailable(exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw ProviderUnavailable();
            }

            AlifStatusResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<AlifStatusResponse>(
                    JsonOptions,
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                throw InvalidProviderResponse(exception);
            }

            if (result is null ||
                !string.Equals(result.OrderId, providerOrderId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(result.TransactionId) ||
                string.IsNullOrWhiteSpace(result.Status) ||
                !signatureService.VerifyProviderResponseToken(
                    result.OrderId,
                    result.Status,
                    result.TransactionId,
                    result.Token))
            {
                throw InvalidProviderResponse();
            }

            return result;
        }
    }

    private static void EnsureEnabled(AlifOptions options)
    {
        if (!options.Enabled)
        {
            throw new ApiProblemException(
                StatusCodes.Status503ServiceUnavailable,
                "payment_provider_unavailable",
                "The payment provider is unavailable",
                "PAYMENT_PROVIDER_UNAVAILABLE");
        }
    }

    private static ApiProblemException ProviderUnavailable(Exception? inner = null)
    {
        return new ApiProblemException(
            StatusCodes.Status503ServiceUnavailable,
            "payment_provider_unavailable",
            "The payment provider is unavailable",
            "PAYMENT_PROVIDER_UNAVAILABLE",
            inner is null ? null : "The provider status request could not be completed.");
    }

    private static ApiProblemException InvalidProviderResponse(Exception? inner = null)
    {
        return new ApiProblemException(
            StatusCodes.Status502BadGateway,
            "invalid_payment_provider_response",
            "The payment provider returned an invalid response",
            "INVALID_PAYMENT_PROVIDER_RESPONSE",
            inner is null ? null : "The provider response could not be validated.");
    }
}
