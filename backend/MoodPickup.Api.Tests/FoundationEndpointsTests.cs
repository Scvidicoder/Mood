using System.Buffers;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using MoodPickup.Api.DTOs;
using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.Entities;

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

    [Fact]
    public void SignalRJsonProtocol_SerializesOrderStatusAsAName()
    {
        var protocol = factory.Services
            .GetServices<IHubProtocol>()
            .Single(candidate => candidate.Name == "json");
        var output = new ArrayBufferWriter<byte>();
        protocol.WriteMessage(
            new InvocationMessage(
                "OrderConfirmed",
                [
                    new OrderRealtimeEventDto(
                        Guid.NewGuid(),
                        factory.TimeProvider.GetUtcNow(),
                        Guid.NewGuid(),
                        "MP-20260811-00001",
                        OrderStatus.Confirmed,
                        factory.TimeProvider.GetUtcNow().AddMinutes(20),
                        null)
                ]),
            output);

        var payload = Encoding.UTF8.GetString(output.WrittenSpan)
            .TrimEnd('\u001e');
        using var document = JsonDocument.Parse(payload);

        Assert.Equal(
            "Confirmed",
            document.RootElement
                .GetProperty("arguments")[0]
                .GetProperty("status")
                .GetString());
    }
}
