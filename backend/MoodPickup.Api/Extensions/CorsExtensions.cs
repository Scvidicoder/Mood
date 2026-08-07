namespace MoodPickup.Api.Extensions;

public static class CorsExtensions
{
    public const string PolicyName = "ConfiguredFrontend";

    public static IServiceCollection AddConfiguredCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var origins = (configuration["AllowedOrigins"] ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (origins.Length == 0)
        {
            throw new InvalidOperationException(
                "AllowedOrigins must contain at least one frontend origin.");
        }

        if (origins.Contains("*", StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "AllowedOrigins cannot contain a wildcard origin.");
        }

        if (origins.Any(origin =>
                !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            throw new InvalidOperationException(
                "AllowedOrigins entries must be absolute HTTP or HTTPS origins.");
        }

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }
}
