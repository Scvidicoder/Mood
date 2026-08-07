using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Extensions;

namespace MoodPickup.Api.Infrastructure;

public sealed class DoubleSubmitCsrfFilter(
    IOptions<RefreshTokenOptions> options) : IAsyncAuthorizationFilter
{
    private readonly RefreshTokenOptions _options = options.Value;

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        var cookieToken = request.Cookies[_options.CsrfCookieName];
        var headerToken = request.Headers[_options.CsrfHeaderName].ToString();

        if (!string.IsNullOrWhiteSpace(cookieToken) &&
            !string.IsNullOrWhiteSpace(headerToken) &&
            AuthenticationHashing.FixedTimeEquals(cookieToken, headerToken))
        {
            return Task.CompletedTask;
        }

        var problemDetails = new ProblemDetails
        {
            Type = "csrf_validation_failed",
            Title = "CSRF validation failed",
            Status = StatusCodes.Status403Forbidden,
            Extensions =
            {
                ["code"] = "CSRF_VALIDATION_FAILED",
                ["traceId"] = TraceIdProvider.GetTraceId(context.HttpContext)
            }
        };

        context.Result = new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status403Forbidden,
            ContentTypes = { "application/problem+json" }
        };

        return Task.CompletedTask;
    }
}
