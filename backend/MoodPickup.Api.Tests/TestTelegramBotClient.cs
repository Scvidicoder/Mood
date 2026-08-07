using System.Collections.Concurrent;
using MoodPickup.Api.DTOs.Telegram;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Tests;

public sealed class TestTelegramBotClient : ITelegramBotClient
{
    private readonly ConcurrentQueue<TelegramSendMessageRequest> _messages = new();

    public IReadOnlyCollection<TelegramSendMessageRequest> Messages =>
        _messages.ToArray();

    public Task<TelegramUserDto> GetMeAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(
            new TelegramUserDto(1, true, "test_bot"));
    }

    public Task SetWebhookAsync(
        Uri webhookUri,
        string secretToken,
        bool dropPendingUpdates,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<TelegramWebhookInfoDto> GetWebhookInfoAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            new TelegramWebhookInfoDto(
                "https://api.example.test/api/v1/telegram/webhook",
                false,
                0,
                null,
                null));
    }

    public Task<TelegramSentMessageDto> SendMessageAsync(
        TelegramSendMessageRequest request,
        CancellationToken cancellationToken)
    {
        _messages.Enqueue(request);
        return Task.FromResult(new TelegramSentMessageDto(_messages.Count));
    }

    public void Clear()
    {
        _messages.Clear();
    }
}
