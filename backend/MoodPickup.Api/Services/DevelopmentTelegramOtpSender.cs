using MoodPickup.Api.Entities;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class DevelopmentTelegramOtpSender(
    ILogger<DevelopmentTelegramOtpSender> logger) : ITelegramOtpSender
{
    public bool AllowsUnlinkedPhoneNumbers => true;

    public Task SendAsync(
        LoginChallenge challenge,
        string oneTimeCode,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Development Telegram OTP for challenge {ChallengeId}: {OneTimeCode}",
            challenge.Id,
            oneTimeCode);

        return Task.CompletedTask;
    }
}
