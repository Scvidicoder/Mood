namespace MoodPickup.Api.Entities;

public sealed class OrderDailySequence
{
    public DateOnly OrderDate { get; set; }

    public int LastValue { get; set; }
}
