using System.Buffers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Media;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;
using SkiaSharp;

namespace MoodPickup.Api.Services;

public sealed class MediaService(
    MoodPickupDbContext dbContext,
    IMediaStorage storage,
    ICurrentUserContext currentUser,
    IEmployeeAuditService auditService,
    IOptions<MediaStorageOptions> options,
    ILogger<MediaService> logger) : IMediaService
{
    private const int CopyBufferSize = 81920;
    private readonly MediaStorageOptions _options = options.Value;

    public async Task<MediaImageDto> UploadImageAsync(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw Validation("file", "Select a non-empty image file.");
        }

        if (file.Length > _options.MaximumFileSizeBytes)
        {
            throw Validation(
                "file",
                $"The image cannot exceed {_options.MaximumFileSizeBytes} bytes.");
        }

        var submittedContentType = file.ContentType.Trim().ToLowerInvariant();
        if (!_options.AllowedContentTypes.Contains(
                submittedContentType,
                StringComparer.OrdinalIgnoreCase))
        {
            throw UnsupportedImage();
        }

        await using var uploadedContent = new MemoryStream(
            checked((int)Math.Min(file.Length, _options.MaximumFileSizeBytes)));
        await CopyWithLimitAsync(
            file,
            uploadedContent,
            _options.MaximumFileSizeBytes,
            cancellationToken);
        uploadedContent.Position = 0;

        var uploadedBytes = uploadedContent.ToArray();
        SKEncodedImageFormat encodedFormat;
        int width;
        int height;
        int frameCount;
        byte[] normalizedBytes;
        try
        {
            using var data = SKData.CreateCopy(uploadedBytes);
            using var codec = SKCodec.Create(data) ?? throw UnsupportedImage();
            encodedFormat = codec.EncodedFormat;
            width = codec.Info.Width;
            height = codec.Info.Height;
            frameCount = codec.FrameCount;

            if (width > _options.MaximumImageWidth ||
                height > _options.MaximumImageHeight)
            {
                throw Validation(
                    "file",
                    $"The image dimensions cannot exceed " +
                    $"{_options.MaximumImageWidth}x{_options.MaximumImageHeight} pixels.");
            }

            if (frameCount > 1)
            {
                throw Validation(
                    "file",
                    "Animated or multi-frame images are not supported.");
            }

            var decodedBytes = checked((long)width * height * 4);
            if (decodedBytes > _options.MaximumDecodedImageBytes)
            {
                throw Validation(
                    "file",
                    "The decoded image is too large to process safely.");
            }

            var decodeInfo = new SKImageInfo(
                width,
                height,
                SKColorType.Rgba8888,
                SKAlphaType.Premul);
            using var bitmap = new SKBitmap(decodeInfo);
            var decodeResult = codec.GetPixels(decodeInfo, bitmap.GetPixels());
            if (decodeResult != SKCodecResult.Success)
            {
                throw Validation(
                    "file",
                    "The image content is malformed or cannot be decoded.");
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var normalized = image.Encode(
                encodedFormat,
                encodedFormat == SKEncodedImageFormat.Png ? 100 : 90);
            normalizedBytes = normalized?.ToArray()
                ?? throw Validation(
                    "file",
                    "The image could not be normalized safely.");
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
            OverflowException)
        {
            throw UnsupportedImage();
        }

        var verifiedContentType = ContentTypeFor(encodedFormat);
        if (!_options.AllowedContentTypes.Contains(
                verifiedContentType,
                StringComparer.OrdinalIgnoreCase) ||
            !string.Equals(
                submittedContentType,
                verifiedContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            throw UnsupportedImage();
        }

        await using var normalizedContent = new MemoryStream(normalizedBytes);
        StoredMediaObject? storedObject = null;
        try
        {
            storedObject = await storage.SaveImageAsync(
                normalizedContent,
                ExtensionFor(verifiedContentType),
                cancellationToken);
            var media = new MediaFile
            {
                Id = Guid.NewGuid(),
                StorageProvider = storage.ProviderName,
                StorageKey = storedObject.StorageKey,
                OriginalFileName = SafeOriginalFileName(file.FileName),
                ContentType = verifiedContentType,
                FileSizeBytes = storedObject.FileSizeBytes,
                Width = width,
                Height = height,
                CreatedByEmployeeId = currentUser.GetRequiredEmployeeId()
            };

            dbContext.MediaFiles.Add(media);
            await auditService.RecordAsync(
                "MediaImageUploaded",
                "MediaFile",
                media.Id,
                $"Uploaded menu image '{media.OriginalFileName}'.",
                null,
                new
                {
                    media.Id,
                    media.OriginalFileName,
                    media.ContentType,
                    media.FileSizeBytes,
                    media.Width,
                    media.Height,
                    media.StorageProvider
                },
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToDto(media);
        }
        catch
        {
            if (storedObject is not null)
            {
                try
                {
                    await storage.DeleteAsync(
                        storedObject.StorageKey,
                        CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    logger.LogError(
                        cleanupException,
                        "Failed to remove media object {StorageKey} after metadata persistence failed.",
                        storedObject.StorageKey);
                }
            }

            throw;
        }
    }

    public async Task<MediaDownload?> OpenImageAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        MediaFile? media;
        try
        {
            media = await dbContext.MediaFiles
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.StorageProvider == storage.ProviderName &&
                        item.StorageKey == storageKey,
                    cancellationToken);
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (media is null)
        {
            return null;
        }

        Stream? stream;
        try
        {
            stream = await storage.OpenReadAsync(storageKey, cancellationToken);
        }
        catch (ArgumentException)
        {
            return null;
        }

        return stream is null
            ? null
            : new MediaDownload(
                stream,
                media.ContentType,
                $"\"{media.Id:N}\"");
    }

    private MediaImageDto ToDto(MediaFile media)
    {
        return new MediaImageDto(
            media.Id,
            media.OriginalFileName,
            media.ContentType,
            media.FileSizeBytes,
            media.Width!.Value,
            media.Height!.Value,
            storage.GetPublicUrl(media.StorageKey),
            media.CreatedAt);
    }

    private static async Task CopyWithLimitAsync(
        IFormFile file,
        Stream destination,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var source = file.OpenReadStream();
        var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        long total = 0;
        try
        {
            int read;
            while ((read = await source.ReadAsync(
                       buffer.AsMemory(0, buffer.Length),
                       cancellationToken)) > 0)
            {
                total += read;
                if (total > maximumBytes)
                {
                    throw Validation(
                        "file",
                        $"The image cannot exceed {maximumBytes} bytes.");
                }

                await destination.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string SafeOriginalFileName(string submittedName)
    {
        var leafName = Path.GetFileName(submittedName.Replace('\\', '/')).Trim();
        var sanitized = new string(leafName
            .Where(character => !char.IsControl(character))
            .ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "image";
        }

        return sanitized.Length <= 255 ? sanitized : sanitized[..255];
    }

    private static string ExtensionFor(string contentType)
    {
        return contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw UnsupportedImage()
        };
    }

    private static string ContentTypeFor(SKEncodedImageFormat format)
    {
        return format switch
        {
            SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Webp => "image/webp",
            _ => throw UnsupportedImage()
        };
    }

    private static ApiValidationException Validation(
        string field,
        string message)
    {
        return new ApiValidationException(
            new Dictionary<string, string[]>
            {
                [field] = [message]
            });
    }

    private static ApiProblemException UnsupportedImage()
    {
        return new ApiProblemException(
            StatusCodes.Status400BadRequest,
            "validation_error",
            "Request validation failed",
            "UNSUPPORTED_IMAGE_FORMAT",
            "Only valid JPEG, PNG, and WebP images are accepted.",
            new Dictionary<string, object?>
            {
                ["errors"] = new Dictionary<string, string[]>
                {
                    ["file"] = ["Only valid JPEG, PNG, and WebP images are accepted."]
                }
            });
    }
}
