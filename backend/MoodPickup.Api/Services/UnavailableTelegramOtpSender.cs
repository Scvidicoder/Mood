using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class UnavailableTelegramOtpSender : ITelegramOtpSender
{
    public bool AllowsUnlinkedPhoneNumbers => false;

    public Task SendAsync(
        LoginChallenge challenge,
        string oneTimeCode,
        CancellationToken cancellationToken)
    {
        throw new ApiProblemException(
            StatusCodes.Status503ServiceUnavailable,
            "telegram_unavailable",
            "Telegram authentication is temporarily unavailable",
            "TELEGRAM_UNAVAILABLE");
    }
}
