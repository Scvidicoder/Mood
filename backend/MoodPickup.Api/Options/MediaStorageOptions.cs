namespace MoodPickup.Api.Options;

public sealed class MediaStorageOptions
{
    public const string SectionName = "MediaStorage";

    public string Provider { get; init; } = "Local";

    public string RootPath { get; init; } = "uploads";

    public string PublicBasePath { get; init; } = "/media";

    public long MaximumFileSizeBytes { get; init; } = 5 * 1024 * 1024;

    public string[] AllowedContentTypes { get; init; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    public int MaximumImageWidth { get; init; } = 6000;

    public int MaximumImageHeight { get; init; } = 6000;

    public long MaximumDecodedImageBytes { get; init; } = 256 * 1024 * 1024;
}
