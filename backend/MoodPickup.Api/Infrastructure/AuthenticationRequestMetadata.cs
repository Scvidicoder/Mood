namespace MoodPickup.Api.Infrastructure;

public sealed record AuthenticationRequestMetadata(
    string IpAddress,
    string UserAgent)
{
    public static AuthenticationRequestMetadata FromHttpContext(HttpContext context)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = context.Request.Headers.UserAgent.ToString();

        return new AuthenticationRequestMetadata(
            ipAddress,
            string.IsNullOrWhiteSpace(userAgent) ? "unknown" : userAgent);
    }
}
