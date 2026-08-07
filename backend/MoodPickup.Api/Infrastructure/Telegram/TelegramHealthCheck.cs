using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Options;
using MoodPickup.Api.Services.Telegram;

namespace MoodPickup.Api.Infrastructure.Telegram;

public sealed class TelegramHealthCheck(
    IOptions<TelegramOptions> options,
    TelegramStartupState startupState) : IHealthCheck
{
    private readonly TelegramOptions _options = options.Value;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled ||
            _options.UseDevelopmentSender ||
            !_options.RegisterWebhookOnStartup)
        {
            return Task.FromResult(
                HealthCheckResult.Healthy(
                    startupState.Snapshot().Description));
        }

        var snapshot = startupState.Snapshot();
        return Task.FromResult(
            snapshot.IsReady
                ? HealthCheckResult.Healthy(
                    snapshot.Description,
                    new Dictionary<string, object>
                    {
                        ["checkedAt"] = snapshot.CheckedAt?.ToString("O") ?? string.Empty
                    })
                : HealthCheckResult.Unhealthy(snapshot.Description));
    }
}
