namespace MoodPickup.Api.Entities;

public sealed class PaymentWebhookEvent
{
    public Guid Id { get; set; }

    public PaymentProvider Provider { get; set; }

    public string EventIdentifier { get; set; } = string.Empty;

    public string PayloadHash { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public string ProcessingResult { get; set; } = string.Empty;
}
