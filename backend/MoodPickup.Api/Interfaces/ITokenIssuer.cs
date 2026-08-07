using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Interfaces;

public interface ITokenIssuer
{
    IssuedAccessToken IssueCustomerAccessToken(Customer customer);

    IssuedAccessToken IssueEmployeeAccessToken(
        Employee employee,
        IReadOnlyCollection<string> roles);

    string IssueRegistrationToken(string phoneNumber, Guid challengeId);

    RegistrationTokenClaims ValidateRegistrationToken(string token);
}

public sealed record IssuedAccessToken(string Value, int ExpiresInSeconds);

public sealed record RegistrationTokenClaims(string PhoneNumber, Guid ChallengeId);
