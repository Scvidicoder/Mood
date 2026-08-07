namespace MoodPickup.Api.Interfaces;

public interface IPasswordPolicyValidator
{
    IReadOnlyCollection<string> Validate(string password, string? username = null);
}
