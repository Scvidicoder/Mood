namespace MoodPickup.Api.Infrastructure.Telegram;

public sealed class TelegramApiException(
    string methodName,
    bool isRetryable,
    int? telegramErrorCode = null)
    : Exception($"Telegram API method {methodName} failed.")
{
    public string MethodName { get; } = methodName;

    public bool IsRetryable { get; } = isRetryable;

    public int? TelegramErrorCode { get; } = telegramErrorCode;
}
