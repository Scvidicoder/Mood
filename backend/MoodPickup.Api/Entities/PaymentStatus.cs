namespace MoodPickup.Api.Entities;

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Cancelled,
    RefundRequired,
    RefundPending,
    Refunded,
    ReconciliationRequired
}
