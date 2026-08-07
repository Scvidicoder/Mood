using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersionNeutral]
[Route("media")]
[Tags("Public Media")]
public sealed class MediaController(IMediaService mediaService) : ControllerBase
{
    [HttpGet("{**storageKey}")]
    [ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Get(
        string? storageKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return NotFound();
        }

        var media = await mediaService.OpenImageAsync(storageKey, cancellationToken);
        if (media is null)
        {
            return NotFound();
        }

        Response.Headers[HeaderNames.CacheControl] = "public,max-age=31536000,immutable";
        Response.Headers[HeaderNames.ETag] = media.ETag;
        return File(media.Content, media.ContentType, enableRangeProcessing: true);
    }
}
