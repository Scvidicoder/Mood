using MoodPickup.Api.DTOs.Telegram;

namespace MoodPickup.Api.Interfaces;

public interface ITelegramUpdateHandler
{
    Task HandleAsync(
        TelegramUpdateDto update,
        CancellationToken cancellationToken);
}
