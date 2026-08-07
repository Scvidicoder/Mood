namespace MoodPickup.Api.Tests;

public sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public TestTimeProvider()
    {
        _utcNow = DateTimeOffset.UtcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration)
    {
        _utcNow = _utcNow.Add(duration);
    }

    public void Reset()
    {
        _utcNow = DateTimeOffset.UtcNow;
    }
}
