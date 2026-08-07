namespace MoodPickup.Api.DTOs;

public sealed record SystemInfoResponse(
    string Service,
    string Environment,
    string ApiVersion,
    DateTimeOffset UtcTime);
