using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryResearchPolicyTests
{
    [Fact]
    public void DatasetRequiresExactMeasurementProvenance()
    {
        var command = Dataset(Profile(source: "Different Research"));
        Assert.Throws<ArgumentException>(() => InventoryResearchPolicy.Validate(command));
    }

    [Fact]
    public void PortfolioCannotClaimMoreDeduplicatedReachThanGrossMeasuredReach()
    {
        var command = Dataset(Profile(), secondProfile: Profile(reach: 80), deduplicatedReach: 250);
        Assert.Throws<ArgumentException>(() => InventoryResearchPolicy.Validate(command));
    }

    [Fact]
    public void CompatibleMeasuredPortfolioPassesValidation()
    {
        var command = Dataset(Profile(), secondProfile: Profile(reach: 80), deduplicatedReach: 150);
        InventoryResearchPolicy.Validate(command);
    }

    private static RegisterInventoryResearchDatasetCommand Dataset(
        InventoryAudienceProfileValues firstProfile,
        InventoryAudienceProfileValues? secondProfile = null,
        decimal deduplicatedReach = 0)
    {
        var observations = new List<InventoryResearchObservationInput>
        {
            new("research:one", "OOH", Guid.NewGuid(), "P-1", null, "Johannesburg", firstProfile),
        };
        var portfolios = new List<InventoryResearchPortfolioInput>();
        if (secondProfile is not null)
        {
            observations.Add(new("research:two", "OOH", Guid.NewGuid(), "P-2", null,
                "Johannesburg", secondProfile));
            portfolios.Add(new("research:portfolio", ["research:one", "research:two"],
                deduplicatedReach, "PEOPLE"));
        }
        return new("Authorised Research", "Fixture release", "2026 Q1",
            "Weighted panel methodology", "Johannesburg adults 18+", "fixture:rights",
            null, null, "Synthetic fixture only", observations, portfolios);
    }

    private static InventoryAudienceProfileValues Profile(
        string source = "Authorised Research", decimal reach = 120) => new(
        [new("English", 80)], [], [new("Business decision makers", 60)], [],
        null, null, "Johannesburg adults 18+", source, "2026 Q1",
        "Weighted panel methodology", "Synthetic fixture only",
        [new("REACH", reach, "PEOPLE", "Johannesburg adults 18+", source,
            "2026 Q1", "Weighted panel methodology", "Synthetic fixture only")]);
}
