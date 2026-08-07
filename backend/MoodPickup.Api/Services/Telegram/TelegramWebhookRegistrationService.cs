using Microsoft.Extensions.Options;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Services.Telegram;

public sealed class TelegramWebhookRegistrationService(
    ITelegramBotClient botClient,
    IOptions<TelegramOptions> options,
    IHostEnvironment environment,
    TelegramStartupState startupState,
    TimeProvider timeProvider,
    ILogger<TelegramWebhookRegistrationService> logger) : IHostedService
{
    private readonly TelegramOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            startupState.MarkReady(
                "Telegram startup registration is disabled in Testing.",
                timeProvider.GetUtcNow());
            return;
        }

        if (_options.UseDevelopmentSender)
        {
            startupState.MarkReady(
                "Development Telegram sender is active.",
                timeProvider.GetUtcNow());
            return;
        }

        if (!_options.Enabled)
        {
            startupState.MarkReady(
                "Telegram integration is disabled.",
                timeProvider.GetUtcNow());
            return;
        }

        if (!_options.RegisterWebhookOnStartup)
        {
            startupState.MarkReady(
                "Real Telegram mode is configured; automatic webhook registration is disabled.",
                timeProvider.GetUtcNow());
            return;
        }

        try
        {
            var bot = await botClient.GetMeAsync(cancellationToken);
            if (!bot.IsBot ||
                !string.Equals(
                    bot.Username,
                    _options.NormalizedBotUsername,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Configured Telegram bot username does not match getMe.");
            }

            var webhookUri = _options.BuildWebhookUri();
            await botClient.SetWebhookAsync(
                webhookUri,
                _options.WebhookSecret,
                _options.DropPendingUpdatesOnRegistration,
                cancellationToken);
            var webhookInfo = await botClient.GetWebhookInfoAsync(cancellationToken);
            if (!Uri.TryCreate(webhookInfo.Url, UriKind.Absolute, out var registeredUri) ||
                registeredUri != webhookUri)
            {
                throw new InvalidOperationException(
                    "Telegram reported an unexpected webhook URL after registration.");
            }

            startupState.MarkReady(
                "Telegram bot and webhook registration are ready.",
                timeProvider.GetUtcNow());
            logger.LogInformation(
                "Telegram webhook registered for bot {BotUsername} at {WebhookUrl}. Pending updates: {PendingUpdateCount}.",
                _options.NormalizedBotUsername,
                webhookUri,
                webhookInfo.PendingUpdateCount);

            if (!string.IsNullOrWhiteSpace(webhookInfo.LastErrorMessage))
            {
                logger.LogWarning(
                    "Telegram webhook reports a previous delivery error at Unix time {LastErrorDate}.",
                    webhookInfo.LastErrorDate);
            }
        }
        catch (Exception exception)
        {
            startupState.MarkFailed(
                "Telegram bot or webhook registration failed.",
                timeProvider.GetUtcNow());
            logger.LogError(
                "Telegram webhook registration failed due to {FailureType}.",
                exception.GetType().Name);
            throw new InvalidOperationException(
                "Telegram webhook registration failed. Review safe Telegram configuration and connectivity.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
