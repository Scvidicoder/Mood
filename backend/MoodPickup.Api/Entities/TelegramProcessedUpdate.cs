namespace MoodPickup.Api.Entities;

public sealed class TelegramProcessedUpdate
{
    public long UpdateId { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}
