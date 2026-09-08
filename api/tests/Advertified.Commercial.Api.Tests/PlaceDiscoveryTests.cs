using System.Net;
using Advertified.Commercial.Infrastructure.Inventory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class PlaceDiscoveryTests
{
    [Fact]
    public async Task SearchUsesSouthAfricanPoiBoundaryAndCachesExactQuery()
    {
        var handler = new FixtureHandler();
        using var client = new HttpClient(handler);
        using var provider = new NominatimPlaceDiscoveryProvider(client, Options.Create(new PlaceDiscoveryOptions
        {
            Enabled = true, PublicServicePolicyAccepted = true,
            UserAgent = "Advertified-Deterministic-Test/1.0",
        }), TimeProvider.System);
        var first = await provider.SearchAsync("Clicks synthetic branch", CancellationToken.None);
        var second = await provider.SearchAsync("Clicks synthetic branch", CancellationToken.None);
        Assert.True(first.Available);
        var place = Assert.Single(first.Places);
        Assert.Equal(28.04m, place.Longitude);
        Assert.Equal(-26.2m, place.Latitude);
        Assert.Equal("https://www.openstreetmap.org/node/123", place.SourceLocator);
        Assert.Contains("verify", place.GeometryBasis);
        Assert.Equal(first.Places, second.Places);
        Assert.Equal(1, handler.Calls);
        Assert.Contains("countrycodes=za", handler.Url);
        Assert.Contains("layer=poi", handler.Url);
        Assert.Contains("limit=10", handler.Url);
        Assert.Contains("q=Clicks%20synthetic%20branch", handler.Url);
        Assert.Equal("Advertified-Deterministic-Test/1.0", handler.UserAgent);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task DisabledOrUnacceptedPublicServiceNeverCallsProvider(bool enabled, bool accepted)
    {
        var handler = new FixtureHandler();
        using var client = new HttpClient(handler);
        using var provider = new NominatimPlaceDiscoveryProvider(client, Options.Create(new PlaceDiscoveryOptions
        {
            Enabled = enabled, PublicServicePolicyAccepted = accepted, UserAgent = "Advertified-Test/1.0",
        }), TimeProvider.System);
        Assert.False((await provider.SearchAsync("Dis-Chem synthetic", CancellationToken.None)).Available);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("us", "-26.2", "28.04")]
    [InlineData("za", "-91", "28.04")]
    [InlineData("za", "-26.2", "181")]
    public void ForeignOrInvalidCoordinatesDoNotBecomeBranchEvidence(string country, string lat, string lon)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new[] { new
        {
            osm_type = "node", osm_id = 123, name = "Synthetic", display_name = "Synthetic place",
            lat, lon, address = new { country_code = country },
        } });
        Assert.Empty(NominatimPlaceParser.Parse(json, DateTimeOffset.UtcNow));
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string Url { get; private set; } = "";
        public string UserAgent { get; private set; } = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            Url = request.RequestUri!.AbsoluteUri;
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"osm_type":"node","osm_id":123,"name":"Clicks synthetic branch","display_name":"Synthetic area, South Africa","lat":"-26.2","lon":"28.04","address":{"country_code":"za"}}]"""),
            });
        }
    }
}
