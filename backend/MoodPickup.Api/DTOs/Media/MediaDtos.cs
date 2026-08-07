namespace MoodPickup.Api.DTOs.Media;

public sealed record MediaImageDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    int Width,
    int Height,
    string Url,
    DateTimeOffset CreatedAt);
