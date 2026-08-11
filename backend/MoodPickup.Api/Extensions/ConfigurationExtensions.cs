using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Extensions;

public static class ConfigurationExtensions
{
    public static IServiceCollection AddValidatedConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddOptions<ConnectionStringsOptions>()
            .Bind(configuration.GetSection(ConnectionStringsOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.DefaultConnection),
                "ConnectionStrings:DefaultConnection must be configured.")
            .ValidateOnStart();

        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<SwaggerOptions>()
            .Bind(configuration.GetSection(SwaggerOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required.")
            .Validate(
                options => options.SigningKey.Length >= 32,
                "Jwt:SigningKey must contain at least 32 characters.")
            .Validate(
                options => options.AccessTokenLifetimeMinutes == 15,
                "Jwt:AccessTokenLifetimeMinutes must be 15 for the current API contract.")
            .Validate(
                options => options.RegistrationTokenLifetimeMinutes is > 0 and <= 15,
                "Jwt:RegistrationTokenLifetimeMinutes must be between 1 and 15.")
            .Validate(
                options => options.ClockSkewSeconds is >= 0 and <= 60,
                "Jwt:ClockSkewSeconds must be between 0 and 60.")
            .ValidateOnStart();

        services
            .AddOptions<RefreshTokenOptions>()
            .Bind(configuration.GetSection(RefreshTokenOptions.SectionName))
            .Validate(options => options.CustomerLifetimeDays > 0, "Customer refresh lifetime must be positive.")
            .Validate(options => options.EmployeeLifetimeDays > 0, "Employee refresh lifetime must be positive.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CookieName), "Refresh cookie name is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CsrfCookieName), "CSRF cookie name is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CsrfHeaderName), "CSRF header name is required.")
            .Validate(
                options => options.CookiePath.StartsWith("/api/", StringComparison.Ordinal),
                "RefreshToken:CookiePath must be restricted to API authentication routes.")
            .Validate(
                options => options.CsrfCookiePath == "/",
                "RefreshToken:CsrfCookiePath must be '/' so the frontend can read the double-submit token.")
            .ValidateOnStart();

        services
            .AddOptions<OtpOptions>()
            .Bind(configuration.GetSection(OtpOptions.SectionName))
            .Validate(options => options.HashKey.Length >= 32, "Otp:HashKey must contain at least 32 characters.")
            .Validate(options => options.LifetimeMinutes == 5, "Otp:LifetimeMinutes must be 5.")
            .Validate(options => options.MaximumAttempts == 5, "Otp:MaximumAttempts must be 5.")
            .Validate(options => options.ResendDelaySeconds == 60, "Otp:ResendDelaySeconds must be 60.")
            .Validate(options => options.PhoneRequestsPerHour > 0, "OTP phone rate limit must be positive.")
            .Validate(options => options.IpRequestsPerHour > 0, "OTP IP rate limit must be positive.")
            .Validate(
                options => options.TelegramChatRequestsPerHour > 0,
                "OTP Telegram-chat rate limit must be positive.")
            .ValidateOnStart();

        services
            .AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.SectionName))
            .Validate(
                options => !options.UseDevelopmentSender || environment.IsDevelopment(),
                "Telegram:UseDevelopmentSender may be true only in Development.")
            .Validate(
                options =>
                    !options.Enabled && !options.UseDevelopmentSender ||
                    IsValidBotUsername(options.NormalizedBotUsername),
                "Telegram:BotUsername must contain 5-32 letters, digits, or underscores.")
            .Validate(
                options =>
                    !IsRealTelegramMode(options) ||
                    !string.IsNullOrWhiteSpace(options.BotToken),
                "Telegram:BotToken is required in real Telegram mode.")
            .Validate(
                options =>
                    !IsRealTelegramMode(options) ||
                    IsValidWebhookSecret(options.WebhookSecret),
                "Telegram:WebhookSecret must contain 1-256 letters, digits, underscores, or hyphens.")
            .Validate(
                options =>
                    !IsRealTelegramMode(options) ||
                    IsValidPublicBaseUrl(options.PublicBaseUrl, environment),
                "Telegram:PublicBaseUrl must contain only an allowed scheme and host; HTTPS is required outside Development.")
            .Validate(
                options =>
                    options.WebhookPath == TelegramOptions.DefaultWebhookPath,
                $"Telegram:WebhookPath must be {TelegramOptions.DefaultWebhookPath} for the current API contract.")
            .Validate(
                options =>
                    options.LinkExpirationMinutes is > 0 and <= 15,
                "Telegram:LinkExpirationMinutes must be between 1 and 15.")
            .Validate(
                options =>
                    options.MaximumContactMismatchAttempts is > 0 and <= 10,
                "Telegram:MaximumContactMismatchAttempts must be between 1 and 10.")
            .Validate(
                options => options.ProcessedUpdateRetentionHours is >= 24 and <= 720,
                "Telegram:ProcessedUpdateRetentionHours must be between 24 and 720.")
            .Validate(
                options => options.ApiTimeoutSeconds is >= 2 and <= 60,
                "Telegram:ApiTimeoutSeconds must be between 2 and 60.")
            .Validate(
                options =>
                    options.MaximumWebhookBodyBytes ==
                    TelegramOptions.DefaultMaximumWebhookBodyBytes,
                "Telegram:MaximumWebhookBodyBytes must be 65536.")
            .Validate(
                options =>
                    !IsRealTelegramMode(options) ||
                    IsValidOtpTemplate(options.OtpMessageTemplate),
                "Telegram:OtpMessageTemplate must safely contain the {0} OTP placeholder.")
            .ValidateOnStart();

        services
            .AddOptions<PasswordPolicyOptions>()
            .Bind(configuration.GetSection(PasswordPolicyOptions.SectionName))
            .Validate(options => options.MinimumLength >= 12, "PasswordPolicy:MinimumLength must be at least 12.")
            .Validate(
                options => options.CommonPasswords
                    .Concat(options.AdditionalCommonPasswords)
                    .Any(password => !string.IsNullOrWhiteSpace(password)),
                "At least one common-password denylist entry must be configured.")
            .ValidateOnStart();

        services
            .AddOptions<CheckoutOptions>()
            .Bind(configuration.GetSection(CheckoutOptions.SectionName))
            .Validate(
                options => Regex.IsMatch(options.Currency, "^[A-Za-z]{3}$"),
                "Checkout:Currency must be a three-letter ISO-style currency code.")
            .Validate(
                options => IsKnownTimeZone(options.TimeZoneId),
                "Checkout:TimeZoneId must identify an available server time zone.")
            .Validate(
                options => IsValidTimeRange(options.OpeningTime, options.ClosingTime),
                "Checkout:OpeningTime and Checkout:ClosingTime must use HH:mm and form a same-day range.")
            .Validate(
                options => options.SchedulingWindowHours == 4,
                "Checkout:SchedulingWindowHours must be 4 for the current API contract.")
            .Validate(
                options => options.PickupIntervalMinutes == 15,
                "Checkout:PickupIntervalMinutes must be 15 for the current API contract.")
            .ValidateOnStart();

        services
            .AddOptions<AdministratorSeedOptions>()
            .Bind(configuration.GetSection(AdministratorSeedOptions.SectionName))
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Username),
                "AdministratorSeed:Username is required when seeding is enabled.")
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.FullName),
                "AdministratorSeed:FullName is required when seeding is enabled.")
            .Validate(
                options => !options.Enabled || options.Password.Length >= 12,
                "AdministratorSeed:Password must contain at least 12 characters when seeding is enabled.")
            .ValidateOnStart();

        services
            .AddOptions<MediaStorageOptions>()
            .Bind(configuration.GetSection(MediaStorageOptions.SectionName))
            .Validate(
                options => string.Equals(
                    options.Provider,
                    "Local",
                    StringComparison.OrdinalIgnoreCase),
                "MediaStorage:Provider must be Local for the current implementation.")
            .Validate(
                options => IsSafeMediaRoot(options.RootPath),
                "MediaStorage:RootPath must be a safe path.")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.PublicBasePath) &&
                    options.PublicBasePath.StartsWith('/') &&
                    options.PublicBasePath.Length > 1 &&
                    !options.PublicBasePath.Contains("..", StringComparison.Ordinal) &&
                    !options.PublicBasePath.EndsWith('/'),
                "MediaStorage:PublicBasePath must be an absolute URL path without traversal or a trailing slash.")
            .Validate(
                options => options.MaximumFileSizeBytes is > 0 and <= 50 * 1024 * 1024,
                "MediaStorage:MaximumFileSizeBytes must be between 1 and 52428800.")
            .Validate(
                options =>
                    options.AllowedContentTypes.Length > 0 &&
                    options.AllowedContentTypes.All(contentType =>
                        contentType is "image/jpeg" or "image/png" or "image/webp"),
                "MediaStorage:AllowedContentTypes may contain only image/jpeg, image/png, and image/webp.")
            .Validate(
                options => options.MaximumImageWidth is > 0 and <= 20000,
                "MediaStorage:MaximumImageWidth must be between 1 and 20000.")
            .Validate(
                options => options.MaximumImageHeight is > 0 and <= 20000,
                "MediaStorage:MaximumImageHeight must be between 1 and 20000.")
            .Validate(
                options => options.MaximumDecodedImageBytes is > 0 and <= 1024L * 1024 * 1024,
                "MediaStorage:MaximumDecodedImageBytes must be between 1 and 1073741824.")
            .ValidateOnStart();

        return services;
    }

    private static bool IsSafeMediaRoot(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) ||
            rootPath is "." or ".." ||
            rootPath.Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries)
                .Contains("..", StringComparer.Ordinal))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(rootPath);
            return !string.Equals(
                fullPath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                Path.GetPathRoot(fullPath)?.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or
            PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsRealTelegramMode(TelegramOptions options)
    {
        return options.Enabled && !options.UseDevelopmentSender;
    }

    private static bool IsValidBotUsername(string username)
    {
        return Regex.IsMatch(
            username,
            @"^[A-Za-z][A-Za-z0-9_]{4,31}$",
            RegexOptions.CultureInvariant);
    }

    private static bool IsValidWebhookSecret(string secret)
    {
        return secret.Length is >= 1 and <= 256 &&
               Regex.IsMatch(
                   secret,
                   @"^[A-Za-z0-9_-]+$",
                   RegexOptions.CultureInvariant);
    }

    private static bool IsValidPublicBaseUrl(
        string publicBaseUrl,
        IHostEnvironment environment)
    {
        if (!Uri.TryCreate(
                publicBaseUrl.Trim().TrimEnd('/'),
                UriKind.Absolute,
                out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            uri.AbsolutePath != "/")
        {
            return false;
        }

        return environment.IsDevelopment()
            ? uri.Scheme is "http" or "https"
            : uri.Scheme == Uri.UriSchemeHttps;
    }

    private static bool IsValidOtpTemplate(string template)
    {
        if (string.IsNullOrWhiteSpace(template) ||
            template.Length > 1000 ||
            !template.Contains("{0}", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            _ = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                template,
                "000000");
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsKnownTimeZone(string timeZoneId)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static bool IsValidTimeRange(string openingTime, string closingTime)
    {
        return TimeOnly.TryParseExact(
                   openingTime,
                   "HH:mm",
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None,
                   out var opening) &&
               TimeOnly.TryParseExact(
                   closingTime,
                   "HH:mm",
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None,
                   out var closing) &&
               opening < closing;
    }
}

public sealed class ConnectionStringsOptions
{
    public const string SectionName = "ConnectionStrings";

    public string DefaultConnection { get; init; } = string.Empty;
}

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public bool ApplyMigrationsOnStartup { get; init; }
}

public sealed class SwaggerOptions
{
    public const string SectionName = "Swagger";

    public bool EnabledInProduction { get; init; }
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    public int RegistrationTokenLifetimeMinutes { get; init; } = 10;

    public int ClockSkewSeconds { get; init; } = 30;
}

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    public int CustomerLifetimeDays { get; init; } = 30;

    public int EmployeeLifetimeDays { get; init; } = 30;

    public string CookieName { get; init; } = "__Secure-MoodPickup.Refresh";

    public string CsrfCookieName { get; init; } = "__Secure-MoodPickup.Csrf";

    public string CsrfHeaderName { get; init; } = "X-CSRF-TOKEN";

    public string CookiePath { get; init; } = "/api/v1/auth";

    public string CsrfCookiePath { get; init; } = "/";
}

public sealed class OtpOptions
{
    public const string SectionName = "Otp";

    public string HashKey { get; init; } = string.Empty;

    public int LifetimeMinutes { get; init; } = 5;

    public int MaximumAttempts { get; init; } = 5;

    public int ResendDelaySeconds { get; init; } = 60;

    public int PhoneRequestsPerHour { get; init; } = 5;

    public int IpRequestsPerHour { get; init; } = 10;

    public int TelegramChatRequestsPerHour { get; init; } = 5;
}

public sealed class PasswordPolicyOptions
{
    public const string SectionName = "PasswordPolicy";

    public int MinimumLength { get; init; } = 12;

    public string[] CommonPasswords { get; init; } = [];

    public string[] AdditionalCommonPasswords { get; init; } = [];
}

public sealed class AdministratorSeedOptions
{
    public const string SectionName = "AdministratorSeed";

    public bool Enabled { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;
}
