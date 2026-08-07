using System.Text;
using System.Text.RegularExpressions;

namespace MoodPickup.Api.Infrastructure;

public static partial class PhoneNumberNormalizer
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var input = value.Trim();
        var builder = new StringBuilder(input.Length + 1);
        foreach (var character in input)
        {
            if (character is ' ' or '-' or '(' or ')' or '.')
            {
                continue;
            }

            builder.Append(character);
        }

        var compact = builder.ToString();
        if (compact.StartsWith("00", StringComparison.Ordinal))
        {
            compact = $"+{compact[2..]}";
        }
        else if (!compact.StartsWith('+'))
        {
            compact = $"+{compact}";
        }

        if (!InternationalPhoneRegex().IsMatch(compact))
        {
            return false;
        }

        normalized = compact;
        return true;
    }

    [GeneratedRegex(@"^\+[1-9]\d{7,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex InternationalPhoneRegex();
}
