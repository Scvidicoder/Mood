using System.Security.Cryptography;

namespace MoodPickup.Api.Services;

public sealed class TemporaryPasswordGenerator
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%&*-_+?";
    private const int PasswordLength = 18;

    public string Generate()
    {
        var all = Uppercase + Lowercase + Digits + Symbols;
        var characters = new char[PasswordLength];
        characters[0] = Pick(Uppercase);
        characters[1] = Pick(Lowercase);
        characters[2] = Pick(Digits);
        characters[3] = Pick(Symbols);

        for (var index = 4; index < characters.Length; index++)
        {
            characters[index] = Pick(all);
        }

        for (var index = characters.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[swapIndex]) =
                (characters[swapIndex], characters[index]);
        }

        return new string(characters);
    }

    private static char Pick(string characters)
    {
        return characters[RandomNumberGenerator.GetInt32(characters.Length)];
    }
}
