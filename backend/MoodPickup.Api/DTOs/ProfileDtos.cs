namespace MoodPickup.Api.DTOs;

public sealed record CustomerProfileDto(
    string Name,
    string PhoneNumber,
    bool PhoneVerified,
    bool TelegramLinked,
    DateTimeOffset RegistrationDate,
    int ActiveOrderCount,
    int CompletedOrderCount,
    Guid RowVersion);

public sealed record UpdateCustomerProfileRequest(
    string Name,
    Guid RowVersion);
