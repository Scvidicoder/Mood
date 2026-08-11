namespace MoodPickup.Api.Entities;

public enum OrderStatus
{
    PendingConfirmation,
    Confirmed,
    Preparing,
    ReadyForPickup,
    Completed,
    Cancelled,
    Rejected
}
