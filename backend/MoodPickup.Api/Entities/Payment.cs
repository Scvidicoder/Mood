namespace MoodPickup.Api.Entities;

public sealed class Payment : IHasTimestamps, IHasConcurrencyToken
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public PaymentProvider Provider { get; set; }

    public string ProviderOrderId { get; set; } = string.Empty;

    public string? ProviderTransactionId { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    public DateTimeOffset? RefundedAt { get; set; }

    public DateTimeOffset? LastVerifiedAt { get; set; }

    public string? FailureReason { get; set; }

    public Guid RowVersion { get; set; }

    public ICollection<PaymentAttempt> Attempts { get; set; } = [];
}
