using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class CampaignCombinationAssessmentTests
{
    [Fact]
    public void CombinationCoversChannelsAndRequiredPlacesWithinActualSupplierCostCeilings()
    {
        var north = Guid.NewGuid();
        var south = Guid.NewGuid();
        var candidates = new[] { Candidate("OOH", 80, [north, south], [north]),
            Candidate("OOH", 30, [north, south], [north]), Candidate("DOOH", 50, [north, south], [south]) };
        var result = CampaignCombinationAssessment.Evaluate(candidates, Mix(100));
        Assert.Equal(2, result.Alternatives.Count);
        foreach (var alternative in result.Alternatives)
        {
            Assert.Equal(2, alternative.CandidateIds.Count);
            Assert.Equal(2, alternative.CoveredRequirementIds.Count);
            Assert.True(alternative.CampaignSupplierCostMinor <= 200);
            Assert.All(alternative.ChannelCosts, item => Assert.True(item.SupplierCostMinor <= item.BudgetMinor));
            Assert.Contains("campaignCombination.uniqueReachAndDuplication", alternative.EvidenceGaps);
            Assert.Contains("campaignCombination.clientPriceNotAssessed", alternative.EvidenceGaps);
        }
    }

    [Fact]
    public void IndividuallyAffordableProductsCannotJointlyOverspendChannelBudget()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var candidates = new[] { Candidate("OOH", 60, [first, second], [first]),
            Candidate("OOH", 60, [first, second], [second]), Candidate("DOOH", 10, [first, second], []) };
        var result = CampaignCombinationAssessment.Evaluate(candidates, Mix(100));
        Assert.Empty(result.Alternatives);
        Assert.False(result.SearchTruncated);
    }

    [Fact]
    public void MissingCostCannotFallBackToCheapListedRate()
    {
        var result = CampaignCombinationAssessment.Evaluate(
            [Candidate("OOH", null, [], []), Candidate("DOOH", 10, [], [])], Mix(100));
        Assert.Empty(result.Alternatives);
        Assert.Equal(1, result.MissingCostCandidateCount);
    }

    [Fact]
    public void CandidateInputOrderDoesNotChangeAlternatives()
    {
        var candidates = new[] { Candidate("OOH", 20, [], []), Candidate("OOH", 30, [], []),
            Candidate("DOOH", 40, [], []) };
        var first = CampaignCombinationAssessment.Evaluate(candidates, Mix(100));
        var second = CampaignCombinationAssessment.Evaluate(candidates.Reverse().ToArray(), Mix(100));
        Assert.Equal(first.Alternatives.SelectMany(item => item.CandidateIds),
            second.Alternatives.SelectMany(item => item.CandidateIds));
    }

    [Fact]
    public void SearchPruningIsVisibleAndNeverProvesInventoryImpossible()
    {
        var candidates = Enumerable.Range(0, 520).Select(_ => Candidate("OOH", 10, [], [])).ToArray();
        var result = CampaignCombinationAssessment.Evaluate(candidates, Mix(100));
        Assert.True(result.SearchTruncated);
        Assert.Equal(512, result.CandidatesConsidered);
        Assert.Empty(result.Alternatives);
    }

    [Fact]
    public void EvidenceBackedOptionPrecedesCheapUnknownAndComparisonShowsExactSwap()
    {
        var cheap = Candidate("OOH", 10, [], []);
        var evidenced = Candidate("OOH", 50, [], []);
        evidenced = evidenced with { Suitability = evidenced.Suitability! with
        {
            BuyAssessment = evidenced.Suitability!.BuyAssessment! with { IsTargetAudience = true, Reach = 100m },
        } };
        var screen = Candidate("DOOH", 20, [], []);
        var result = CampaignCombinationAssessment.Evaluate([cheap, evidenced, screen], Mix(100));
        Assert.Contains(evidenced.Id, result.Alternatives[0].CandidateIds);
        Assert.Equal(1, result.Alternatives[0].Comparison!.MeasuredTargetCandidateCount);
        Assert.Equal(1, result.Alternatives[0].Comparison!.MissingDeliveryCandidateCount);
        var comparison = result.Alternatives[1].Comparison!;
        Assert.Equal(-40, comparison.SupplierCostDeltaMinor);
        Assert.Equal(cheap.Id, Assert.Single(comparison.AddedCandidateIds));
        Assert.Equal(evidenced.Id, Assert.Single(comparison.RemovedCandidateIds));
        Assert.Equal(["Presence", "Timed messages"], comparison.PlannedChannelRoles);
    }

    [Fact]
    public void KnownCreativeSlotMismatchCannotBecomeAnAlternative()
    {
        var screen = Candidate("DOOH", 20, [], []);
        screen = screen with { Suitability = screen.Suitability! with
        {
            BuyAssessment = screen.Suitability!.BuyAssessment! with { DigitalExposure = new(15, 5, 60, 1, 8.3333m) },
        } };
        var result = CampaignCombinationAssessment.Evaluate([Candidate("OOH", 10, [], []), screen], Mix(100));
        Assert.Empty(result.Alternatives);
    }

    private static MediaMixVersionView Mix(long channelBudget) => new(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), 1, channelBudget * 2, "ZAR", [new("OOH", channelBudget, "Presence", []),
            new("DOOH", channelBudget, "Timed messages", [])], [], "fixture", "APPROVED", Guid.NewGuid(),
        Guid.NewGuid(), 1, DateTimeOffset.UnixEpoch);

    private static InventoryShortlistCandidateView Candidate(string channel, long? cost,
        Guid[] required, Guid[] covered) => new(
        Id: Guid.NewGuid(), InventoryTenantId: Guid.NewGuid(), MarketplaceListingVersionId: null,
        InventoryProductId: Guid.NewGuid(), ProductVersionId: Guid.NewGuid(), RateId: Guid.NewGuid(),
        AvailabilityId: Guid.NewGuid(), Name: "Fixture placement", Channel: channel, Geography: "Fixture",
        RateAmountMinor: 1, Currency: "ZAR", IsEligible: true, RejectionReason: null, RejectionDetail: null,
        Score: .5m, AudienceFit: new(null, null, null, []), Rationale: null, IsSelected: null, Benchmark: null,
        SpatialMatch: new(true, required, covered, [], [], [], [], 1m, []),
        Suitability: InventorySuitabilityScorer.Empty(PlanningPolicy.Load()) with
        {
            BuyAssessment = new(cost, "ZAR", null, null, null, null, null, null, null, null, null, false, null, []),
        });
}
