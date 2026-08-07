using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Interfaces;

public interface ITelegramOtpSender
{
    bool AllowsUnlinkedPhoneNumbers { get; }

    Task SendAsync(
        LoginChallenge challenge,
        string oneTimeCode,
        CancellationToken cancellationToken);
}
