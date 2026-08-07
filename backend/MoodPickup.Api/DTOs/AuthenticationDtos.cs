using System.Text.Json.Serialization;

namespace MoodPickup.Api.DTOs;

public sealed record RequestCustomerCodeRequest(string PhoneNumber);

public sealed record RequestCustomerCodeResponse(
    Guid ChallengeId,
    int ExpiresInSeconds,
    int ResendAvailableInSeconds,
    string TelegramBotUrl,
    string ClientChallengeSecret,
    CustomerChallengeStatus Status);

public sealed record CustomerChallengeStatusRequest(
    Guid ChallengeId,
    string ClientChallengeSecret);

public sealed record CustomerChallengeStatusResponse(
    CustomerChallengeStatus Status,
    int ExpiresInSeconds,
    bool CanResend);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CustomerChallengeStatus
{
    WaitingForTelegramStart,
    WaitingForTelegramContact,
    OtpSent,
    Expired,
    Locked,
    Completed
}

public sealed record VerifyCustomerCodeRequest(Guid ChallengeId, string Code);

public sealed record CompleteCustomerRegistrationRequest(
    string RegistrationToken,
    string Name);

public sealed record CustomerSummary(
    Guid Id,
    string Name,
    string PhoneNumber);

public sealed record CustomerVerificationResponse(
    bool IsNewCustomer,
    string? AccessToken,
    int? ExpiresInSeconds,
    CustomerSummary? Customer,
    string? RegistrationToken);

public sealed record CustomerAuthenticationResponse(
    string AccessToken,
    int ExpiresInSeconds,
    CustomerSummary Customer);

public sealed record RefreshSessionResponse(
    string AccessToken,
    int ExpiresInSeconds);

public sealed record EmployeeLoginRequest(
    string Username,
    string Password);

public sealed record ChangeEmployeePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public sealed record EmployeeSummary(
    Guid Id,
    string FullName,
    string Username,
    IReadOnlyCollection<string> Roles);

public sealed record EmployeeAuthenticationResponse(
    string AccessToken,
    int ExpiresInSeconds,
    bool MustChangePassword,
    EmployeeSummary Employee);
