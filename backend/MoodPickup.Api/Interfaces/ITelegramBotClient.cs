using MoodPickup.Api.DTOs.Telegram;

namespace MoodPickup.Api.Interfaces;

public interface ITelegramBotClient
{
    Task<TelegramUserDto> GetMeAsync(CancellationToken cancellationToken);

    Task SetWebhookAsync(
        Uri webhookUri,
        string secretToken,
        bool dropPendingUpdates,
        CancellationToken cancellationToken);

    Task<TelegramWebhookInfoDto> GetWebhookInfoAsync(
        CancellationToken cancellationToken);

    Task<TelegramSentMessageDto> SendMessageAsync(
        TelegramSendMessageRequest request,
        CancellationToken cancellationToken);
}
