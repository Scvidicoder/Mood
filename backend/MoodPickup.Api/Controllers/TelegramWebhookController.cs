using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MoodPickup.Api.DTOs.Telegram;
using MoodPickup.Api.Infrastructure.Telegram;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/telegram/webhook")]
[ServiceFilter<TelegramWebhookSecretFilter>]
[EnableRateLimiting("telegram-webhook")]
[RequestSizeLimit(TelegramOptions.DefaultMaximumWebhookBodyBytes)]
public sealed class TelegramWebhookController(
    ITelegramUpdateHandler updateHandler) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Receive(
        TelegramUpdateDto update,
        CancellationToken cancellationToken)
    {
        await updateHandler.HandleAsync(update, cancellationToken);
        return Ok();
    }
}
