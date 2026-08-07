using System.Collections.Concurrent;

namespace MoodPickup.Api.Services.Telegram;

public sealed class TelegramAbuseLimiter(TimeProvider timeProvider)
{
    private static readonly TimeSpan StartWindow = TimeSpan.FromMinutes(10);
    private const int MaximumStartAttempts = 12;
    private readonly ConcurrentDictionary<long, AttemptWindow> _starts = new();

    public TelegramLimitResult ConsumeStartAttempt(long telegramUserId)
    {
        var now = timeProvider.GetUtcNow();
        var window = _starts.AddOrUpdate(
            telegramUserId,
            _ => new AttemptWindow(now, 1),
            (_, current) =>
                current.StartedAt.Add(StartWindow) <= now
                    ? new AttemptWindow(now, 1)
                    : current with { Count = current.Count + 1 });

        return window.Count switch
        {
            <= MaximumStartAttempts => TelegramLimitResult.Allowed,
            MaximumStartAttempts + 1 => TelegramLimitResult.LimitReached,
            _ => TelegramLimitResult.Blocked
        };
    }

    private sealed record AttemptWindow(DateTimeOffset StartedAt, int Count);
}

public enum TelegramLimitResult
{
    Allowed,
    LimitReached,
    Blocked
}
