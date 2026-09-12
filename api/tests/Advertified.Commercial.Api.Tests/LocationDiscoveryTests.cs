using System.Net;
using Advertified.Commercial.Infrastructure.LocationIntelligence;
using Microsoft.Extensions.Options;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class LocationDiscoveryTests
{
    private static readonly string[] SyntheticBounds = ["-27", "-25", "27", "29"];

    [Fact]
    public async Task SearchUsesSouthAfricanBoundaryReturnsBoundsAndCachesExactQuery()
    {
        var handler = new FixtureHandler(ValidPlaceJson());
        using var client = new HttpClient(handler);
        using var provider = new NominatimLocationDiscoveryProvider(
            client,
            Options.Create(new LocationDiscoveryOptions
            {
                PublicServicePolicyAccepted = true,
                UserAgent = "Advertified-Deterministic-Test/1.0",
            }),
            TimeProvider.System);

        var first = await provider.SearchAsync("Gauteng", CancellationToken.None);
        var second = await provider.SearchAsync("Gauteng", CancellationToken.None);

        var place = Assert.Single(first);
        Assert.Equal(28.04m, place.Longitude);
        Assert.Equal(-26.2m, place.Latitude);
        Assert.Equal("https://www.openstreetmap.org/relation/123", place.SourceLocator);
        Assert.NotNull(place.Bounds);
        Assert.Equal(-27m, place.Bounds!.South);
        Assert.Equal(-25m, place.Bounds.North);
        Assert.Equal(27m, place.Bounds.West);
        Assert.Equal(29m, place.Bounds.East);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.Calls);
        Assert.Contains("countrycodes=za", handler.Url);
        Assert.Contains("addressdetails=1", handler.Url);
        Assert.Contains("limit=10", handler.Url);
        Assert.Contains("q=Gauteng", handler.Url);
        Assert.Equal("Advertified-Deterministic-Test/1.0", handler.UserAgent);
    }

    [Fact]
    public async Task UnacceptedPublicServiceNeverCallsProvider()
    {
        var handler = new FixtureHandler(ValidPlaceJson());
        using var client = new HttpClient(handler);
        using var provider = new NominatimLocationDiscoveryProvider(
            client,
            Options.Create(new LocationDiscoveryOptions
            {
                PublicServicePolicyAccepted = false,
                UserAgent = "Advertified-Test/1.0",
            }),
            TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.SearchAsync("Gauteng", CancellationToken.None));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("us", "-26.2", "28.04")]
    [InlineData("za", "-91", "28.04")]
    [InlineData("za", "-26.2", "181")]
    public async Task ForeignOrInvalidCoordinatesDoNotBecomeLocationFacts(
        string country,
        string lat,
        string lon)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new[] { new
        {
            osm_type = "node",
            osm_id = 123,
            name = "Synthetic",
            display_name = "Synthetic place",
            lat,
            lon,
            boundingbox = SyntheticBounds,
            address = new { country_code = country },
        } });
        var handler = new FixtureHandler(json);
        using var client = new HttpClient(handler);
        using var provider = new NominatimLocationDiscoveryProvider(
            client,
            Options.Create(new LocationDiscoveryOptions
            {
                PublicServicePolicyAccepted = true,
                UserAgent = "Advertified-Test/1.0",
            }),
            TimeProvider.System);

        Assert.Empty(await provider.SearchAsync("Synthetic", CancellationToken.None));
    }

    private static string ValidPlaceJson() =>
        """[{"osm_type":"relation","osm_id":123,"name":"Gauteng","display_name":"Gauteng, South Africa","lat":"-26.2","lon":"28.04","boundingbox":["-27","-25","27","29"],"address":{"country_code":"za"}}]""";

    private sealed class FixtureHandler(string responseJson) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string Url { get; private set; } = "";
        public string UserAgent { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Url = request.RequestUri!.AbsoluteUri;
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson),
            });
        }
    }
}
