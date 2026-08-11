using System.Globalization;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Services;

internal static class EstimatedReadyTimeRules
{
    public static DateTimeOffset Validate(
        DateTimeOffset value,
        CheckoutOptions options,
        DateTimeOffset now)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var localValue = TimeZoneInfo.ConvertTime(value, timeZone);
        var opening = TimeOnly.ParseExact(
            options.OpeningTime,
            "HH:mm",
            CultureInfo.InvariantCulture);
        var closing = TimeOnly.ParseExact(
            options.ClosingTime,
            "HH:mm",
            CultureInfo.InvariantCulture);
        var localTime = TimeOnly.FromDateTime(localValue.DateTime);
        var errors = new List<string>();

        if (localValue.Date != localNow.Date)
        {
            errors.Add("Estimated ready time must be today in the cafe time zone.");
        }

        if (localTime < opening || localTime >= closing)
        {
            errors.Add(
                $"Estimated ready time must be within business hours ({options.OpeningTime}-{options.ClosingTime}).");
        }

        if (value.ToUniversalTime() <= now)
        {
            errors.Add("Estimated ready time must be after the current time.");
        }

        if (errors.Count > 0)
        {
            throw new ApiValidationException(new Dictionary<string, string[]>
            {
                ["estimatedReadyTime"] = errors.ToArray()
            });
        }

        return value.ToUniversalTime();
    }
}
