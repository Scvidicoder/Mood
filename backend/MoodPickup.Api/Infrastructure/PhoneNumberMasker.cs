namespace MoodPickup.Api.Infrastructure;

public static class PhoneNumberMasker
{
    public static string Mask(string phoneNumber)
    {
        if (phoneNumber.Length <= 4)
        {
            return "****";
        }

        return $"{phoneNumber[..Math.Min(4, phoneNumber.Length)]}******{phoneNumber[^2..]}";
    }
}
