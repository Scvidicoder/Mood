namespace MoodPickup.Api.Options;

public sealed class CheckoutOptions
{
    public const string SectionName = "Checkout";

    public string Currency { get; init; } = "TJS";

    public string TimeZoneId { get; init; } = "Asia/Dushanbe";

    public string OpeningTime { get; init; } = "10:00";

    public string ClosingTime { get; init; } = "22:00";

    public int PickupIntervalMinutes { get; init; } = 15;
}
