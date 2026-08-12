using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Options;

public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    public PaymentProvider Provider { get; set; } = PaymentProvider.Alif;
}
