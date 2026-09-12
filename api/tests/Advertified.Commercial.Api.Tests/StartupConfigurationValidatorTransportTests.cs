using Advertified.Commercial.Api.Startup;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class StartupConfigurationValidatorTransportTests
{
    [Theory]
    [InlineData("https://runtime.example", true)]
    [InlineData("https://runtime.example:8443/", true)]
    [InlineData("http://agent-runtime:8080", true)]
    [InlineData("http://agent-runtime:8080/", true)]
    [InlineData("http://agent-runtime:8081", false)]
    [InlineData("http://agent-runtime:8080/invoke", false)]
    [InlineData("http://runtime.example:8080", false)]
    [InlineData("http://localhost:8080", false)]
    [InlineData("https://user:password@runtime.example", false)]
    public void ProductionRuntimeTransportIsHttpsOrExactPrivateComposeEndpoint(
        string endpoint,
        bool expected)
    {
        Assert.Equal(
            expected,
            StartupConfigurationValidator.HasSafeAgentRuntimeTransport(endpoint));
    }

    [Theory]
    [InlineData("https://maps.advertified.example/", "nominatim.openstreetmap.org", true)]
    [InlineData("https://poi.advertified.example/api/interpreter", "overpass-api.de", true)]
    [InlineData("https://nominatim.openstreetmap.org/", "nominatim.openstreetmap.org", false)]
    [InlineData("https://overpass-api.de/api/interpreter", "overpass-api.de", false)]
    [InlineData("http://maps.advertified.example/", "nominatim.openstreetmap.org", false)]
    [InlineData("https://user:password@maps.advertified.example/", "nominatim.openstreetmap.org", false)]
    public void ProductionLocationTransportRejectsPublicCommunityAndUnsafeEndpoints(
        string endpoint,
        string publicHost,
        bool expected)
    {
        Assert.Equal(
            expected,
            StartupConfigurationValidator.HasSafeProductionLocationEndpoint(endpoint, publicHost));
    }
}
