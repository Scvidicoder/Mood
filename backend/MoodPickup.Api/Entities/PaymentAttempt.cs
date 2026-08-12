namespace MoodPickup.Api.Entities;

public sealed class PaymentAttempt : IHasCreatedAt
{
    public Guid Id { get; set; }

    public Guid PaymentId { get; set; }

    public Payment Payment { get; set; } = null!;

    public int AttemptNumber { get; set; }

    public string ProviderReference { get; set; } = string.Empty;

    public string ProviderStatus { get; set; } = string.Empty;

    public string RequestSnapshot { get; set; } = string.Empty;

    public string? ResponseSnapshot { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
