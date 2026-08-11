using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoodPickup.Api.DTOs.Media;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = AuthenticationConstants.Policies.CanManageProducts)]
[Route("api/v{version:apiVersion}/admin/media")]
[Tags("Admin Menu - Media")]
public sealed class AdminMediaController(IMediaService mediaService) : ControllerBase
{
    [HttpPost("images")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<MediaImageDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MediaImageDto>> UploadImage(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var result = await mediaService.UploadImageAsync(file, cancellationToken);
        return Created(result.Url, result);
    }
}
