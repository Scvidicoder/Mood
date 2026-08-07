using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Data;

namespace MoodPickup.Api.Extensions;

public static class MigrationExtensions
{
    public static async Task ApplyDevelopmentMigrationsAsync(this WebApplication app)
    {
        var databaseOptions = app.Services
            .GetRequiredService<IOptions<DatabaseOptions>>()
            .Value;

        if (!databaseOptions.ApplyMigrationsOnStartup)
        {
            return;
        }

        if (!app.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Automatic migrations are permitted only in Development.");
        }

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MoodPickupDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
