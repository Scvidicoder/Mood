using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoodPickup.Api.DTOs.Telegram;

public sealed record TelegramApiEnvelope<T>(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("result")] T? Result,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("error_code")] int? ErrorCode);

public sealed record TelegramUserDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("is_bot")] bool IsBot,
    [property: JsonPropertyName("username")] string? Username);

public sealed record TelegramWebhookInfoDto(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("has_custom_certificate")] bool HasCustomCertificate,
    [property: JsonPropertyName("pending_update_count")] int PendingUpdateCount,
    [property: JsonPropertyName("last_error_date")] long? LastErrorDate,
    [property: JsonPropertyName("last_error_message")] string? LastErrorMessage);

public sealed record TelegramUpdateDto(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramMessageDto? Message);

public sealed record TelegramMessageDto(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("from")] TelegramUserDto? From,
    [property: JsonPropertyName("chat")] TelegramChatDto Chat,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("contact")] TelegramContactDto? Contact);

public sealed record TelegramChatDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("type")] string Type);

public sealed record TelegramContactDto(
    [property: JsonPropertyName("phone_number")] string PhoneNumber,
    [property: JsonPropertyName("user_id")] long? UserId);

public sealed record TelegramSetWebhookRequest(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("secret_token")] string SecretToken,
    [property: JsonPropertyName("allowed_updates")] IReadOnlyCollection<string> AllowedUpdates,
    [property: JsonPropertyName("drop_pending_updates")] bool DropPendingUpdates);

public sealed record TelegramSendMessageRequest(
    [property: JsonPropertyName("chat_id")] long ChatId,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("reply_markup")] JsonElement? ReplyMarkup = null)
{
    public TelegramSendMessageRequest(
        long chatId,
        string text,
        object? replyMarkup)
        : this(
            chatId,
            text,
            replyMarkup is null
                ? null
                : JsonSerializer.SerializeToElement(
                    replyMarkup,
                    TelegramJson.Options))
    {
    }
}

public sealed record TelegramReplyKeyboardMarkup(
    [property: JsonPropertyName("keyboard")] IReadOnlyCollection<IReadOnlyCollection<TelegramKeyboardButton>> Keyboard,
    [property: JsonPropertyName("resize_keyboard")] bool ResizeKeyboard = true,
    [property: JsonPropertyName("one_time_keyboard")] bool OneTimeKeyboard = true);

public sealed record TelegramKeyboardButton(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("request_contact")] bool RequestContact);

public sealed record TelegramReplyKeyboardRemove(
    [property: JsonPropertyName("remove_keyboard")] bool RemoveKeyboard = true);

public sealed record TelegramSentMessageDto(
    [property: JsonPropertyName("message_id")] long MessageId);

public static class TelegramJson
{
    public static readonly JsonSerializerOptions Options =
        new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
}
