using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Data;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Services.Telegram;

public sealed class TelegramProcessedUpdateCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<TelegramOptions> options,
    IHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<TelegramProcessedUpdateCleanupService> logger) : BackgroundService
{
    private readonly TelegramOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        await DeleteExpiredAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DeleteExpiredAsync(stoppingToken);
        }
    }

    private async Task DeleteExpiredAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext =
                scope.ServiceProvider.GetRequiredService<MoodPickupDbContext>();
            var cutoff = timeProvider.GetUtcNow()
                .AddHours(-_options.ProcessedUpdateRetentionHours);
            var deleted = await dbContext.TelegramProcessedUpdates
                .Where(update => update.ProcessedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
            if (deleted > 0)
            {
                logger.LogInformation(
                    "Deleted {ProcessedUpdateCount} expired Telegram update markers.",
                    deleted);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Telegram update-marker cleanup failed due to {FailureType}.",
                exception.GetType().Name);
        }
    }
}
