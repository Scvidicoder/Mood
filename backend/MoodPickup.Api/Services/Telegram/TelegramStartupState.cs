namespace MoodPickup.Api.Services.Telegram;

public sealed class TelegramStartupState
{
    private readonly object _sync = new();
    private bool _isReady;
    private string _description = "Telegram startup validation has not run.";
    private DateTimeOffset? _checkedAt;

    public void MarkReady(string description, DateTimeOffset checkedAt)
    {
        lock (_sync)
        {
            _isReady = true;
            _description = description;
            _checkedAt = checkedAt;
        }
    }

    public void MarkFailed(string description, DateTimeOffset checkedAt)
    {
        lock (_sync)
        {
            _isReady = false;
            _description = description;
            _checkedAt = checkedAt;
        }
    }

    public TelegramStartupSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new TelegramStartupSnapshot(
                _isReady,
                _description,
                _checkedAt);
        }
    }
}

public sealed record TelegramStartupSnapshot(
    bool IsReady,
    string Description,
    DateTimeOffset? CheckedAt);
