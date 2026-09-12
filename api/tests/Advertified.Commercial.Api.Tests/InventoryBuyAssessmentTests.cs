using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryBuyAssessmentTests
{
    [Fact]
    public void CompatibleSuppliedEvidenceGivesCostAndFrequencyButNeverForecastsReach()
    {
        var result = Assess(Candidate());
        Assert.Equal(300_000L, result.CampaignSupplierCostMinor);
        Assert.Equal(10_000m, result.Reach);
        Assert.Equal(30_000m, result.Impressions);
        Assert.Equal(3m, result.AverageFrequency);
        Assert.Equal(10_000m, result.CostPerThousandImpressionsMinor);
        Assert.Equal(30m, result.CostPerPersonReachedMinor);
        Assert.True(result.IsTargetAudience);
        Assert.Equal("BUY", result.Decision!.Code);
        Assert.Contains("buyDecision.measuredTargetAudience", result.Decision.SupportedReasons);
        Assert.Contains("buyAssessment.measurementNotForecast", result.EvidenceGaps);
    }

    [Theory]
    [InlineData("2025-09-01/2025-09-30", "fixture:study", "Target mothers")]
    [InlineData("September 2026", "fixture:study", "Target mothers")]
    [InlineData("2026-09-01/2026-09-30", "", "Target mothers")]
    [InlineData("2026-09-01/2026-09-30", "fixture:study", "")]
    public void MissingOrStaleEvidenceNeverProducesDeliveryMath(string period, string source, string universe)
    {
        var candidate = Candidate();
        candidate = candidate with { AudienceFit = candidate.AudienceFit with
        {
            DeliveryMeasurements = candidate.AudienceFit.DeliveryMeasurements!
                .Select(item => item with { MeasurementPeriod = period, MeasurementSource = source, Universe = universe }).ToArray(),
        } };
        var result = Assess(candidate);
        Assert.Null(result.Reach);
        Assert.Null(result.AverageFrequency);
        Assert.Null(result.CostPerThousandImpressionsMinor);
        Assert.Equal("NEEDS_REVIEW", result.Decision!.Code);
        Assert.Contains("buyDecision.deliveryEvidenceMissing", result.Decision.BlockingReasons);
    }

    [Fact]
    public void DifferentStudiesCannotBeDividedIntoFrequency()
    {
        var candidate = Candidate();
        candidate = candidate with { AudienceFit = candidate.AudienceFit with
        {
            DeliveryMeasurements = candidate.AudienceFit.DeliveryMeasurements!
                .Select(item => item.MetricType == "REACH" ? item with { MeasurementSource = "fixture:other" } : item).ToArray(),
        } };
        var result = Assess(candidate);
        Assert.Null(result.AverageFrequency);
        Assert.Contains("buyAssessment.incompatibleMeasurements", result.EvidenceGaps);
    }

    [Fact]
    public void DemographicSharesAreNotMultipliedIntoTargetReach()
    {
        var candidate = Candidate();
        candidate = candidate with { AudienceFit = candidate.AudienceFit with
        {
            LanguageScore = .5m, LifeStageScore = .5m, LsmSemScore = .5m,
            DeliveryMeasurements = candidate.AudienceFit.DeliveryMeasurements!
                .Select(item => item with { Universe = "All adults" }).ToArray(),
        } };
        var result = Assess(candidate);
        Assert.Equal(10_000m, result.Reach);
        Assert.False(result.IsTargetAudience);
    }

    [Fact]
    public void PlacementCanContributeToOneRequiredGeographyWithoutCoveringTheWholeCampaign()
    {
        var candidate = Candidate();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        candidate = candidate with
        {
            SpatialMatch = new(true, [first, second], [first], [], [], [], [], .5m, []),
        };

        var result = Assess(candidate);

        Assert.Equal("BUY", result.Decision!.Code);
        Assert.Contains("buyDecision.requiredGeographyContribution", result.Decision.SupportedReasons);
        Assert.DoesNotContain("buyDecision.requiredGeographyMissing", result.Decision.BlockingReasons);
    }

    [Theory]
    [InlineData(5, 60, 1, "8.3333")]
    [InlineData(5, 60, 2, "16.6667")]
    [InlineData(70, 60, 1, null)]
    public void DigitalSlotShareIsNotFiveSecondsOfCampaignDelivery(int slot, int loop, int plays, string? expected)
    {
        var candidate = Candidate();
        candidate = candidate with { Inventory = candidate.Inventory with
        {
            Channel = "DOOH", DeliverableJson = $$"""{"spotLengthSeconds":5,"slotLengthSeconds":{{slot}},"loopLengthSeconds":{{loop}},"playsPerLoop":{{plays}}}""",
        } };
        var result = Assess(candidate);
        Assert.Equal(expected is null ? null : decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            result.DigitalExposure!.LoopSharePercent);
        Assert.Contains("buyAssessment.operatingHoursAndContractedPlays", result.EvidenceGaps);
    }

    private static InventoryBuyAssessmentView Assess(PreparedShortlistCandidate candidate) =>
        InventoryBuyAssessment.Evaluate(candidate, PlanningPolicy.Load(),
            [new(Guid.NewGuid(), "Target mothers", "", null, null, [], null, null, null, null, null,
                "HYPOTHESIS", [], [Guid.NewGuid()], [], 1m, "APPROVED")]);

    [Fact]
    public void PlannerRetainsChannelIntentButFlagsCreativeThatDoesNotFitPurchasedSlot()
    {
        var candidate = Candidate();
        candidate = candidate with { Inventory = candidate.Inventory with
        {
            Channel = "DOOH", DeliverableJson = """{"spotLengthSeconds":15,"slotLengthSeconds":5,"loopLengthSeconds":60,"playsPerLoop":1}""",
        } };
        var assessment = Assess(candidate);
        var result = assessment.PlannerReasoning!;
        Assert.Equal("DO_NOT_BUY", assessment.Decision!.Code);
        Assert.Contains("buyDecision.creativeDoesNotFit", assessment.Decision.BlockingReasons);
        Assert.Equal("Relevant reach", result.PlannedChannelRole);
        Assert.Equal("Target mothers", Assert.Single(result.TargetContexts).Name);
        Assert.Contains("plannerReasoning.measuredTargetBaseline", result.SupportedReasons);
        Assert.Contains("plannerReasoning.creativeDoesNotFit", result.BuyingWarnings);
        Assert.Contains("plannerReasoning.messageAndMoment", result.ReviewQuestions);
    }

    private static PreparedShortlistCandidate Candidate()
    {
        var inventory = new PlanningInventoryRow(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Fixture supplier", "Fixture billboard", "OOH", "OOH_SITE", "Sandton", null, null,
            Guid.NewGuid(), "DAY_RATE", "ZAR", 10_000, new(2026, 9, 1), new(2026, 9, 30), "fixture:rate",
            Guid.NewGuid(), "AVAILABLE", null, null, "fixture:availability", "[]", null,
            "NOT_APPLICABLE", null, "NOT_APPLICABLE", null, null, null, null);
        InventoryDeliveryMeasurementView Measurement(string code, decimal amount, string unit) =>
            new(code, amount, unit, "Target mothers", "fixture:study", "2026-09-01/2026-09-30", "fixture:method", null);
        return new(Guid.NewGuid(), inventory,
            new("OOH", 500_000_000, "Relevant reach", [new(new(2026, 9, 1), new(2026, 9, 30))]),
            new(true, null, null, null), new(null, null, null, [], DeliveryMeasurements:
                [Measurement("REACH", 10_000, "PEOPLE"), Measurement("IMPRESSIONS", 30_000, "COUNT")]),
            new(false, [], [], [], [], [], [], 1m, []), InventorySuitabilityScorer.Empty(PlanningPolicy.Load()),
            "fixture", "fixture", null);
    }
}
