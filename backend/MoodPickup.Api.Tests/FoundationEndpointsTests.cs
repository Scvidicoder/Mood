using System.Net;
using System.Net.Http.Json;
using MoodPickup.Api.DTOs;

namespace MoodPickup.Api.Tests;

public sealed class FoundationEndpointsTests(MoodPickupApiFactory factory)
    : IClassFixture<MoodPickupApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SystemInfo_ReturnsExpectedNonSecretMetadata()
    {
        using var response = await _client.GetAsync("/api/v1/system/info");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<SystemInfoResponse>();

        Assert.NotNull(payload);
        Assert.Equal("MoodPickup.Api", payload.Service);
        Assert.Equal("Testing", payload.Environment);
        Assert.Equal("1.0", payload.ApiVersion);
        Assert.Equal(factory.TimeProvider.GetUtcNow(), payload.UtcTime);
    }

    [Fact]
    public async Task LiveHealth_ReturnsHealthyWithoutDatabaseDependency()
    {
        using var response = await _client.GetAsync("/health/live");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"Healthy\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownRoute_ReturnsProblemDetails()
    {
        using var response = await _client.GetAsync("/api/v1/does-not-exist");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"type\":\"not_found\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"traceId\":", payload, StringComparison.Ordinal);
    }
}
