using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class CampaignScenarioClassifierTests
{
    [Fact]
    public void LabelsOnlyEvidenceBackedExtremesAndKeepsFirstAlternativeRecommended()
    {
        var recommended = Alternative(100, 1_000m, 2m);
        var reach = Alternative(120, 1_500m, 1.8m);
        var frequency = Alternative(90, 900m, 3m);
        var cheaper = Alternative(70, 800m, 1.5m);

        var result = CampaignScenarioClassifier.Attach([recommended, reach, frequency, cheaper]);

        Assert.Equal("RECOMMENDED", result[0].Scenario!.Code);
        Assert.True(result[0].Scenario!.Recommended);
        Assert.Equal("MAX_MEASURED_REACH", result[1].Scenario!.Code);
        Assert.Equal(500m, result[1].Scenario!.DeduplicatedReachDelta);
        Assert.Equal("HIGHER_FREQUENCY", result[2].Scenario!.Code);
        Assert.Equal(1m, result[2].Scenario!.AverageFrequencyDelta);
        Assert.Equal("LOWER_SUPPLIER_COST", result[3].Scenario!.Code);
        Assert.Equal(-30, result[3].Scenario!.SupplierCostDeltaMinor);
    }

    [Fact]
    public void TiesDoNotCreateFalseTradeoffLabels()
    {
        var recommended = Alternative(100, 1_000m, 2m);
        var tied = Alternative(100, 1_000m, 2m);

        var result = CampaignScenarioClassifier.Attach([recommended, tied]);

        Assert.Equal("ALTERNATIVE", result[1].Scenario!.Code);
        Assert.Equal(0, result[1].Scenario!.SupplierCostDeltaMinor);
        Assert.Equal(0m, result[1].Scenario!.DeduplicatedReachDelta);
        Assert.Equal(0m, result[1].Scenario!.AverageFrequencyDelta);
    }

    [Fact]
    public void MissingAudienceEvidenceNeverCreatesReachOrFrequencyClaim()
    {
        var recommended = Alternative(100, null, null);
        var cheaper = Alternative(80, null, null);

        var result = CampaignScenarioClassifier.Attach([recommended, cheaper]);

        Assert.Equal("RECOMMENDED", result[0].Scenario!.Code);
        Assert.Equal("LOWER_SUPPLIER_COST", result[1].Scenario!.Code);
        Assert.Null(result[1].Scenario!.DeduplicatedReachDelta);
        Assert.Null(result[1].Scenario!.AverageFrequencyDelta);
    }

    private static CampaignCombinationView Alternative(long cost, decimal? reach, decimal? frequency) => new(
        [Guid.NewGuid()], cost, cost, "ZAR", [], [], [], AudienceForecast: new(
            GrossReach: reach, DeduplicatedReach: reach, DuplicatedReach: 0,
            TotalImpressions: reach.HasValue && frequency.HasValue ? reach * frequency : null,
            AverageFrequency: frequency, Universe: reach.HasValue ? "Target audience" : null,
            MeasurementPeriod: reach.HasValue ? "2026-09" : null,
            MeasurementSource: reach.HasValue ? "fixture:study" : null,
            Methodology: reach.HasValue ? "fixture:method" : null,
            IncrementalReach: [], EvidenceGaps: []));
}
