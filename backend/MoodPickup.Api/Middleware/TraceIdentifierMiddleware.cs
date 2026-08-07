using MoodPickup.Api.Infrastructure;
using Serilog.Context;

namespace MoodPickup.Api.Middleware;

public sealed class TraceIdentifierMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = TraceIdProvider.GetTraceId(context);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Trace-Id"] = traceId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("TraceId", traceId))
        {
            await next(context);
        }
    }
}
