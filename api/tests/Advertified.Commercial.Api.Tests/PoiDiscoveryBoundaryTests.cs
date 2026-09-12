using Advertified.Commercial.Application.LocationIntelligence;
using Advertified.Commercial.Infrastructure.LocationIntelligence;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class PoiDiscoveryBoundaryTests
{
    [Theory]
    [InlineData("node")]
    [InlineData("way")]
    [InlineData("relation")]
    public void BoundedPoiDoesNotClaimAdministrativeMembership(string kind)
    {
        var anchor = new DiscoveredLocation(
            "osm:relation:100", "Synthetic district", "Synthetic address", -26m, 28m,
            "https://www.openstreetmap.org/relation/100", "Test fixture",
            DateTimeOffset.UnixEpoch, "Test bounds", new LocationBounds(-27m, -25m, 27m, 29m));
        var coordinates = kind == "node"
            ? "\"lat\":-26,\"lon\":28"
            : "\"center\":{\"lat\":-26,\"lon\":28}";
        var json = """
            {"elements":[
              {"type":"KIND","id":1,COORDINATES,"tags":{"name":"Synthetic school"}},
              {"type":"node","id":2,"lat":-28,"lon":28,"tags":{"name":"Outside"}},
              {"type":"node","id":3,"lat":-26,"lon":28,"tags":{"name":"Addressed","addr:city":"Supplied address"}}
            ]}
            """.Replace("KIND", kind, StringComparison.Ordinal)
               .Replace("COORDINATES", coordinates, StringComparison.Ordinal);

        var places = OverpassPoiParser.Parse(json, anchor, 10);

        Assert.Equal(2, places.Count);
        Assert.Equal("Search bounds for Synthetic district; administrative-area membership not verified",
            places[0].DisplayLocation);
        Assert.Contains("administrative-area membership not verified", places[0].GeometryBasis);
        Assert.Equal("Supplied address", places[1].DisplayLocation);
        Assert.DoesNotContain(places, item => item.Name == "Outside");
    }
}
