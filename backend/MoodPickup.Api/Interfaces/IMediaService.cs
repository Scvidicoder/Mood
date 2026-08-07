using MoodPickup.Api.DTOs.Media;

namespace MoodPickup.Api.Interfaces;

public interface IMediaService
{
    Task<MediaImageDto> UploadImageAsync(
        IFormFile? file,
        CancellationToken cancellationToken);

    Task<MediaDownload?> OpenImageAsync(
        string storageKey,
        CancellationToken cancellationToken);
}

public sealed record MediaDownload(
    Stream Content,
    string ContentType,
    string ETag);
