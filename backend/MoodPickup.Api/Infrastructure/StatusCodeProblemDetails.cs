using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MoodPickup.Api.Infrastructure;

public static class StatusCodeProblemDetails
{
    public static async Task WriteAsync(StatusCodeContext statusCodeContext)
    {
        var context = statusCodeContext.HttpContext;
        var response = context.Response;

        if (response.HasStarted ||
            response.ContentLength is not null ||
            !string.IsNullOrWhiteSpace(response.ContentType))
        {
            return;
        }

        var (type, title) = response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => ("unauthorized", "Authentication required"),
            StatusCodes.Status403Forbidden => ("forbidden", "Access denied"),
            StatusCodes.Status404NotFound => ("not_found", "Resource not found"),
            _ => ("http_error", "The request could not be completed")
        };

        var extensions = new Dictionary<string, object?>
        {
            ["traceId"] = TraceIdProvider.GetTraceId(context)
        };
        if (response.StatusCode == StatusCodes.Status403Forbidden)
        {
            extensions["code"] = "FORBIDDEN";
        }

        var result = Results.Problem(new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = response.StatusCode,
            Extensions = extensions
        });

        await result.ExecuteAsync(context);
    }
}
