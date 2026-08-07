namespace MoodPickup.Api.Interfaces;

public interface IDevelopmentMenuSeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}
