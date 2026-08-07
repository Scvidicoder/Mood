using MoodPickup.Api.DTOs.Telegram;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Services.Telegram;

namespace MoodPickup.Api.Services;

public sealed class TelegramOtpSender(
    ITelegramBotClient botClient,
    TelegramMessageProvider messages,
    ILogger<TelegramOtpSender> logger) : ITelegramOtpSender
{
    public bool AllowsUnlinkedPhoneNumbers => false;

    public async Task SendAsync(
        LoginChallenge challenge,
        string oneTimeCode,
        CancellationToken cancellationToken)
    {
        if (challenge.TelegramChatId is not long chatId)
        {
            throw new ApiProblemException(
                StatusCodes.Status409Conflict,
                "telegram_contact_required",
                "Telegram contact verification is required",
                "TELEGRAM_CONTACT_REQUIRED");
        }

        await botClient.SendMessageAsync(
            new TelegramSendMessageRequest(
                chatId,
                messages.Otp(oneTimeCode),
                new TelegramReplyKeyboardRemove()),
            cancellationToken);

        logger.LogInformation(
            "OTP delivered through Telegram for challenge {ChallengeId}.",
            challenge.Id);
    }
}
