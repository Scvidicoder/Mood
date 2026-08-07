namespace MoodPickup.Api.Entities;

public sealed class LoginChallenge : IHasConcurrencyToken
{
    public Guid Id { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string? CodeHash { get; set; }

    public long? TelegramChatId { get; set; }

    public long? TelegramUserId { get; set; }

    public string? TelegramUsername { get; set; }

    public string? TelegramLinkTokenHash { get; set; }

    public DateTimeOffset? TelegramLinkExpiresAt { get; set; }

    public DateTimeOffset? TelegramStartedAt { get; set; }

    public DateTimeOffset? TelegramLinkUsedAt { get; set; }

    public DateTimeOffset? TelegramLinkedAt { get; set; }

    public DateTimeOffset? TelegramContactVerifiedAt { get; set; }

    public int TelegramLinkAttemptCount { get; set; }

    public int TelegramDeliveryFailureCount { get; set; }

    public DateTimeOffset? TelegramDeliveryFailedAt { get; set; }

    public string? ClientStatusSecretHash { get; set; }

    public DateTimeOffset? OtpSentAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public int AttemptCount { get; set; }

    public int MaximumAttempts { get; set; }

    public bool IsUsed { get; set; }

    public DateTimeOffset LastSentAt { get; set; }

    public LoginChallengePurpose Purpose { get; set; }

    public string RequestIpHash { get; set; } = string.Empty;

    public string UserAgentHash { get; set; } = string.Empty;

    public Guid RowVersion { get; set; }
}
