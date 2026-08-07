using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MoodPickup.Api.DTOs.Telegram;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Infrastructure.Telegram;

public sealed class TelegramBotApiClient(
    HttpClient httpClient,
    IOptions<TelegramOptions> options,
    ILogger<TelegramBotApiClient> logger) : ITelegramBotClient
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly TelegramOptions _options = options.Value;

    public Task<TelegramUserDto> GetMeAsync(CancellationToken cancellationToken)
    {
        return SendAsync<TelegramUserDto>(
            "getMe",
            request: null,
            cancellationToken);
    }

    public async Task SetWebhookAsync(
        Uri webhookUri,
        string secretToken,
        bool dropPendingUpdates,
        CancellationToken cancellationToken)
    {
        await SendAsync<bool>(
            "setWebhook",
            new TelegramSetWebhookRequest(
                webhookUri.ToString(),
                secretToken,
                ["message"],
                dropPendingUpdates),
            cancellationToken);
    }

    public Task<TelegramWebhookInfoDto> GetWebhookInfoAsync(
        CancellationToken cancellationToken)
    {
        return SendAsync<TelegramWebhookInfoDto>(
            "getWebhookInfo",
            request: null,
            cancellationToken);
    }

    public Task<TelegramSentMessageDto> SendMessageAsync(
        TelegramSendMessageRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync<TelegramSentMessageDto>(
            "sendMessage",
            request,
            cancellationToken);
    }

    private async Task<T> SendAsync<T>(
        string methodName,
        object? request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = request is null
                ? await httpClient.GetAsync(BuildMethodPath(methodName), cancellationToken)
                : await httpClient.PostAsJsonAsync(
                    BuildMethodPath(methodName),
                    request,
                    SerializerOptions,
                    cancellationToken);

            var envelope = await response.Content
                .ReadFromJsonAsync<TelegramApiEnvelope<T>>(
                    SerializerOptions,
                    cancellationToken);
            if (response.IsSuccessStatusCode &&
                envelope is { Ok: true, Result: not null })
            {
                return envelope.Result;
            }

            var safeDescription = Sanitize(envelope?.Description);
            logger.LogWarning(
                "Telegram API method {MethodName} failed with HTTP {StatusCode}, Telegram code {TelegramErrorCode}: {Description}",
                methodName,
                (int)response.StatusCode,
                envelope?.ErrorCode,
                safeDescription);
            throw new TelegramApiException(
                methodName,
                IsRetryable(response.StatusCode),
                envelope?.ErrorCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TelegramApiException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or
            JsonException or NotSupportedException)
        {
            logger.LogWarning(
                "Telegram API method {MethodName} failed due to {FailureType}.",
                methodName,
                exception.GetType().Name);
            throw new TelegramApiException(methodName, isRetryable: true);
        }
    }

    private string BuildMethodPath(string methodName)
    {
        // The token contains a colon. Prefixing the relative path prevents
        // System.Uri from interpreting "bot123:..." as a custom URI scheme.
        return $"./bot{_options.BotToken}/{methodName}";
    }

    private string Sanitize(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "No safe description was provided.";
        }

        return description.Replace(
            _options.BotToken,
            "[redacted]",
            StringComparison.Ordinal);
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.TooManyRequests ||
               (int)statusCode >= 500;
    }
}
