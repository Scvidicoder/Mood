using Microsoft.Extensions.Options;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Services;

public sealed class LocalMediaStorage : IMediaStorage
{
    private const int CopyBufferSize = 81920;
    private readonly string _rootPath;
    private readonly string _rootPathPrefix;
    private readonly string _publicBasePath;

    public LocalMediaStorage(
        IOptions<MediaStorageOptions> options,
        IWebHostEnvironment environment)
    {
        var configured = options.Value;
        _rootPath = Path.GetFullPath(
            Path.IsPathRooted(configured.RootPath)
                ? configured.RootPath
                : Path.Combine(environment.ContentRootPath, configured.RootPath));
        _rootPathPrefix = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;
        _publicBasePath = configured.PublicBasePath.TrimEnd('/');
        Directory.CreateDirectory(_rootPath);
    }

    public string ProviderName => "Local";

    public async Task<StoredMediaObject> SaveImageAsync(
        Stream content,
        string fileExtension,
        CancellationToken cancellationToken)
    {
        var normalizedExtension = NormalizeExtension(fileExtension);
        var id = Guid.NewGuid().ToString("N");
        var storageKey = $"{id[..2]}/{id[2..4]}/{id}{normalizedExtension}";
        var physicalPath = ResolvePhysicalPath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

        long bytesWritten = 0;
        try
        {
            await using var destination = new FileStream(
                physicalPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[CopyBufferSize];
            int read;
            while ((read = await content.ReadAsync(
                       buffer.AsMemory(0, buffer.Length),
                       cancellationToken)) > 0)
            {
                await destination.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);
                bytesWritten += read;
            }

            await destination.FlushAsync(cancellationToken);
            return new StoredMediaObject(storageKey, bytesWritten);
        }
        catch
        {
            TryDeleteFile(physicalPath);
            throw;
        }
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TryDeleteFile(ResolvePhysicalPath(storageKey));
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var physicalPath = ResolvePhysicalPath(storageKey);
        Stream? stream = File.Exists(physicalPath)
            ? new FileStream(
                physicalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null;
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(ResolvePhysicalPath(storageKey)));
    }

    public string GetPublicUrl(string storageKey)
    {
        ValidateStorageKey(storageKey);
        var encodedSegments = storageKey
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);
        return $"{_publicBasePath}/{string.Join('/', encodedSegments)}";
    }

    private string ResolvePhysicalPath(string storageKey)
    {
        ValidateStorageKey(storageKey);
        var physicalPath = Path.GetFullPath(
            Path.Combine(
                _rootPath,
                storageKey.Replace('/', Path.DirectorySeparatorChar)));

        if (!physicalPath.StartsWith(
                _rootPathPrefix,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            throw new ArgumentException("The media storage key is invalid.", nameof(storageKey));
        }

        return physicalPath;
    }

    private static void ValidateStorageKey(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) ||
            storageKey.Length > 512 ||
            storageKey.Contains('\\', StringComparison.Ordinal) ||
            Path.IsPathRooted(storageKey))
        {
            throw new ArgumentException("The media storage key is invalid.", nameof(storageKey));
        }

        var segments = storageKey.Split('/');
        if (segments.Length < 2 ||
            segments.Any(segment =>
                string.IsNullOrWhiteSpace(segment) ||
                segment is "." or ".." ||
                segment.Any(character =>
                    !(char.IsAsciiLetterOrDigit(character) ||
                      character is '-' or '_' or '.'))))
        {
            throw new ArgumentException("The media storage key is invalid.", nameof(storageKey));
        }
    }

    private static string NormalizeExtension(string fileExtension)
    {
        var extension = fileExtension.ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".png" or ".webp" => extension,
            _ => throw new ArgumentException(
                "The media file extension is unsupported.",
                nameof(fileExtension))
        };
    }

    private static void TryDeleteFile(string physicalPath)
    {
        try
        {
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }
        }
        catch (IOException)
        {
            // Cleanup is best-effort here; callers log persistence cleanup failures.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup is best-effort here; callers log persistence cleanup failures.
        }
    }
}
