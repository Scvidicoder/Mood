using Microsoft.Extensions.Options;
using MoodPickup.Api.Extensions;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class PasswordPolicyValidator : IPasswordPolicyValidator
{
    private readonly PasswordPolicyOptions _options;
    private readonly HashSet<string> _commonPasswords;

    public PasswordPolicyValidator(IOptions<PasswordPolicyOptions> options)
    {
        _options = options.Value;
        _commonPasswords = _options.CommonPasswords
            .Concat(_options.AdditionalCommonPasswords)
            .Select(password => password.Trim())
            .Where(password => password.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> Validate(string password, string? username = null)
    {
        var errors = new List<string>();

        if (password.Length < _options.MinimumLength)
        {
            errors.Add($"Password must contain at least {_options.MinimumLength} characters.");
        }

        if (!password.Any(char.IsUpper))
        {
            errors.Add("Password must contain an uppercase letter.");
        }

        if (!password.Any(char.IsLower))
        {
            errors.Add("Password must contain a lowercase letter.");
        }

        if (!password.Any(char.IsDigit))
        {
            errors.Add("Password must contain a digit.");
        }

        if (!password.Any(character => !char.IsLetterOrDigit(character)))
        {
            errors.Add("Password must contain a special character.");
        }

        if (!string.IsNullOrWhiteSpace(username) &&
            string.Equals(password, username, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Password must not equal the username.");
        }

        if (_commonPasswords.Contains(password))
        {
            errors.Add("Password is too common.");
        }

        return errors;
    }
}
