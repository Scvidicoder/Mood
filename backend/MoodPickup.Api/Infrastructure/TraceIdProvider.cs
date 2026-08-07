using System.Diagnostics;

namespace MoodPickup.Api.Infrastructure;

public static class TraceIdProvider
{
    public static string GetTraceId(HttpContext httpContext)
    {
        return Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
    }
}
