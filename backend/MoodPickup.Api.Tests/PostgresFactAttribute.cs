namespace MoodPickup.Api.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(
                    PostgresMoodPickupApiFactory.ConnectionStringVariable)))
        {
            Skip =
                $"Set {PostgresMoodPickupApiFactory.ConnectionStringVariable} to run PostgreSQL API tests.";
        }
    }
}
