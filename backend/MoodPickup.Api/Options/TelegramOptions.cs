namespace MoodPickup.Api.Options;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";
    public const string DefaultWebhookPath = "/api/v1/telegram/webhook";
    public const int DefaultMaximumWebhookBodyBytes = 64 * 1024;

    public bool Enabled { get; init; }

    public string BotToken { get; init; } = string.Empty;

    public string BotUsername { get; init; } = string.Empty;

    public string WebhookSecret { get; init; } = string.Empty;

    public string PublicBaseUrl { get; init; } = string.Empty;

    public string WebhookPath { get; init; } = DefaultWebhookPath;

    public bool RegisterWebhookOnStartup { get; init; }

    public bool DropPendingUpdatesOnRegistration { get; init; }

    public bool UseDevelopmentSender { get; init; }

    public string OtpMessageTemplate { get; init; } =
        "Номер подтверждён. Ваш код Mood Pickup: {0}\n\nКод действует 5 минут. Никому его не сообщайте.";

    public int LinkExpirationMinutes { get; init; } = 5;

    public int MaximumContactMismatchAttempts { get; init; } = 3;

    public int ProcessedUpdateRetentionHours { get; init; } = 48;

    public int ApiTimeoutSeconds { get; init; } = 10;

    public int MaximumWebhookBodyBytes { get; init; } =
        DefaultMaximumWebhookBodyBytes;

    public string NormalizedBotUsername =>
        BotUsername.Trim().TrimStart('@');

    public Uri BuildWebhookUri()
    {
        var baseUrl = PublicBaseUrl.Trim().TrimEnd('/');
        return new Uri($"{baseUrl}{WebhookPath}", UriKind.Absolute);
    }

    public string BuildBotUrl(string? startToken = null)
    {
        var baseUrl = $"https://t.me/{NormalizedBotUsername}";
        return string.IsNullOrWhiteSpace(startToken)
            ? baseUrl
            : $"{baseUrl}?start={Uri.EscapeDataString(startToken)}";
    }
}
