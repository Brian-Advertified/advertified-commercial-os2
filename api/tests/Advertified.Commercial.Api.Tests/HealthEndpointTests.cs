using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(
            builder => builder.UseDeterministicInventoryProtection());
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Theory]
    [InlineData("/health/live", "healthy")]
    [InlineData("/health/ready", "ready")]
    public async Task HealthEndpointReturnsExpectedState(string route, string expectedState)
    {
        using var response = await _client.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var body = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(body);

        Assert.Equal(expectedState, json.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            "advertified-commercial-api",
            json.RootElement.GetProperty("service").GetString());
    }
}
