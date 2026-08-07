using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Extensions;

namespace MoodPickup.Api.Infrastructure;

public sealed class AuthenticationHashing(IOptions<OtpOptions> options)
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.HashKey);

    public string HashOneTimeCode(Guid challengeId, string oneTimeCode)
    {
        return ComputeKeyedHash($"{challengeId:N}:{oneTimeCode}");
    }

    public string HashMetadata(string value)
    {
        return ComputeKeyedHash(value);
    }

    public string HashTelegramLinkToken(string rawToken)
    {
        return ComputeKeyedHash($"telegram-link:{rawToken}");
    }

    public string HashClientChallengeSecret(string rawSecret)
    {
        return ComputeKeyedHash($"client-challenge:{rawSecret}");
    }

    public static string HashRefreshToken(string rawToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }

    public static string CreateRandomToken(int byteLength = 64)
    {
        return Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            RandomNumberGenerator.GetBytes(byteLength));
    }

    public static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);

        return expectedBytes.Length == actualBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private string ComputeKeyedHash(string value)
    {
        using var hmac = new HMACSHA256(_key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }
}
