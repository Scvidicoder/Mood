namespace MoodPickup.Api.Options;

public sealed class AlifOptions
{
    public const string SectionName = "Alif";

    public bool Enabled { get; init; }

    public string Environment { get; init; } = "Sandbox";

    public bool? UseSandbox { get; init; }

    public string Key { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string CallbackUrl { get; init; } = string.Empty;

    public string ReturnUrl { get; init; } = string.Empty;

    public string Gate { get; init; } = "km";

    public string SandboxBaseUrl { get; init; } = "https://test-web.alif.tj/";

    public string ProductionBaseUrl { get; init; } = "https://web.alif.tj/";

    public int ApiTimeoutSeconds { get; init; } = 15;

    public bool IsSandbox => UseSandbox ??
        string.Equals(Environment, "Sandbox", StringComparison.OrdinalIgnoreCase);

    public string BaseUrl => IsSandbox ? SandboxBaseUrl : ProductionBaseUrl;
}
