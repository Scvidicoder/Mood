using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Infrastructure.Telegram;

public sealed class TelegramWebhookSecretFilter(
    IOptions<TelegramOptions> options) : IAsyncResourceFilter
{
    public const string SecretHeaderName =
        "X-Telegram-Bot-Api-Secret-Token";

    private readonly TelegramOptions _options = options.Value;

    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var bodySizeFeature =
            context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySizeFeature is { IsReadOnly: false })
        {
            bodySizeFeature.MaxRequestBodySize =
                _options.MaximumWebhookBodyBytes;
        }

        if (request.ContentLength > _options.MaximumWebhookBodyBytes)
        {
            context.Result = Problem(
                StatusCodes.Status413PayloadTooLarge,
                "telegram_webhook_too_large",
                "Telegram webhook payload is too large",
                "TELEGRAM_WEBHOOK_TOO_LARGE");
            return;
        }

        var suppliedSecret = request.Headers[SecretHeaderName].ToString();
        if (!SecretMatches(suppliedSecret, _options.WebhookSecret))
        {
            context.Result = Problem(
                StatusCodes.Status401Unauthorized,
                "telegram_webhook_unauthorized",
                "Telegram webhook authentication failed",
                "TELEGRAM_WEBHOOK_UNAUTHORIZED");
            return;
        }

        await next();
    }

    private static bool SecretMatches(string supplied, string configured)
    {
        if (string.IsNullOrEmpty(supplied) ||
            string.IsNullOrEmpty(configured))
        {
            return false;
        }

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        return CryptographicOperations.FixedTimeEquals(
            suppliedHash,
            configuredHash);
    }

    private static ObjectResult Problem(
        int status,
        string type,
        string title,
        string code)
    {
        return new ObjectResult(new ProblemDetails
        {
            Status = status,
            Type = type,
            Title = title,
            Extensions =
            {
                ["code"] = code
            }
        })
        {
            StatusCode = status
        };
    }
}
