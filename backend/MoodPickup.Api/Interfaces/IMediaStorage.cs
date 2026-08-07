namespace MoodPickup.Api.Interfaces;

public interface IMediaStorage
{
    string ProviderName { get; }

    Task<StoredMediaObject> SaveImageAsync(
        Stream content,
        string fileExtension,
        CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(
        string storageKey,
        CancellationToken cancellationToken);

    string GetPublicUrl(string storageKey);
}

public sealed record StoredMediaObject(string StorageKey, long FileSizeBytes);
