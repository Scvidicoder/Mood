using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Services;

public sealed class AlifSignatureService(IOptionsMonitor<AlifOptions> options)
{
    public string CreatePaymentToken(
        string providerOrderId,
        decimal amount,
        string callbackUrl)
    {
        var current = options.CurrentValue;
        return ComputeToken(
            current.Key,
            current.Password,
            current.Key + providerOrderId + FormatAmount(amount) + callbackUrl);
    }

    public string CreateStatusCheckToken(string providerOrderId)
    {
        var current = options.CurrentValue;
        return ComputeToken(
            current.Key,
            current.Password,
            current.Key + providerOrderId);
    }

    public bool VerifyProviderResponseToken(
        string providerOrderId,
        string status,
        string transactionId,
        string suppliedToken)
    {
        var current = options.CurrentValue;
        var expected = ComputeToken(
            current.Key,
            current.Password,
            providerOrderId + status + transactionId);
        return FixedTimeEqualsHex(expected, suppliedToken);
    }

    public static string FormatAmount(decimal amount)
    {
        return amount.ToString("0.00", CultureInfo.InvariantCulture);
    }

    internal static string ComputeToken(
        string key,
        string password,
        string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var derivedPasswordBytes = HMACSHA256.HashData(keyBytes, passwordBytes);
        var derivedPasswordHex = Convert.ToHexString(derivedPasswordBytes).ToLowerInvariant();
        return Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(derivedPasswordHex),
            Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    private static bool FixedTimeEqualsHex(string expected, string supplied)
    {
        if (expected.Length != supplied.Length || supplied.Length % 2 != 0)
        {
            return false;
        }

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expected),
                Convert.FromHexString(supplied));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
