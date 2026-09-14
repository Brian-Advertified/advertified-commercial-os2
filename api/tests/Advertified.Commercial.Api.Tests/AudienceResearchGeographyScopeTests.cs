using Advertified.Commercial.Infrastructure.Intelligence;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class AudienceResearchGeographyScopeTests
{
    [Fact]
    public void CityMarketsExpandToTheirProvinceWithoutReplacingTheBriefGeography()
    {
        var scope = AudienceResearchGeographyScope.Expand([
            "Johannesburg", "Cape Town", "Durban"]);

        Assert.Equal([
            "Johannesburg", "Gauteng",
            "Cape Town", "Western Cape",
            "Durban", "KwaZulu-Natal"], scope);
    }

    [Fact]
    public void ProvinceCodesNormalizeToPublishedProvinceNames()
    {
        var scope = AudienceResearchGeographyScope.Expand(["GP", "WC", "KZN"]);

        Assert.Contains("Gauteng", scope);
        Assert.Contains("Western Cape", scope);
        Assert.Contains("KwaZulu-Natal", scope);
    }
}
