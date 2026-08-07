using MoodPickup.Api.Services;

namespace MoodPickup.Api.Extensions;

public static class AuthenticationSeedExtensions
{
    public static async Task SeedAuthenticationDataAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<AdministratorSeeder>();
        await seeder.SeedAsync(CancellationToken.None);
    }
}
