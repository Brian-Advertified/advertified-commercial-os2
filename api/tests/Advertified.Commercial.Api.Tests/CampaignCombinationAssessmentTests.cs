using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.CommercialSettings;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class CampaignCombinationAssessmentTests
{
    [Fact]
    public void CombinationCoversChannelsAndRequiredPlacesWithinCanonicalClientBudget()
    {
        var north = Guid.NewGuid();
        var south = Guid.NewGuid();
        var candidates = new[] { Candidate("OOH", 80, [north, south], [north]),
            Candidate("OOH", 30, [north, south], [north]), Candidate("DOOH", 50, [north, south], [south]) };
        var result = CampaignCombinationAssessment.Evaluate(candidates, Mix(100), Policy());
        Assert.Equal(2, result.Alternatives.Count);
        foreach (var alternative in result.Alternatives)
        {
            Assert.Equal(2, alternative.CandidateIds.Count);
            Assert.Equal(2, alternative.CoveredRequirementIds.Count);
            Assert.True(alternative.CampaignSupplierCostMinor <= 200);
            Assert.Equal(alternative.CampaignSupplierCostMinor, alternative.CampaignClientPriceMinor);
            Assert.All(alternative.ChannelCosts, item =>
                Assert.True(item.ClientPriceMinor <= item.BudgetMinor));
            Assert.True(result.ClientPriceAssessed);
            Assert.Contains("campaignCombination.uniqueReachAndDuplication", alternative.EvidenceGaps);
            Assert.DoesNotContain("campaignCombination.clientPriceNotAssessed", alternative.EvidenceGaps);
        }
    }

    [Fact]
    public void IndividuallyAffordableProductsCannotJointlyOverspendChannelBudget()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var candidates = new[] { Candidate("OOH", 60, [first, second], [first]),
            Candidate("OOH", 60, [first, second], [second]), Candidate("DOOH", 10, [first, second], []) };
        var result = CampaignCombinationAssessment.Evaluate(candidates, Mix(100), Policy());
        Assert.Empty(result.Alternatives);
        Assert.False(result.SearchTruncated);
    }

    [Fact]
    public void MissingCostCannotFallBackToCheapListedRate()
    {
        var result = CampaignCombinationAssessment.Evaluate(
            [Candidate("OOH", null, [], []), Candidate("DOOH", 10, [], [])],
            Mix(100), Policy());
        Assert.Empty(result.Alternatives);
        Assert.Equal(1, result.MissingCostCandidateCount);
    }

    [Fact]
    public void CandidateInputOrderDoesNotChangeAlternatives()
    {
        var candidates = new[] { Candidate("OOH", 20, [], []), Candidate("OOH", 30, [], []),
            Candidate("DOOH", 40, [], []) };
        var first = CampaignCombinationAssessment.Evaluate(candidates, Mix(100), Policy());
        var second = CampaignCombinationAssessment.Evaluate(candidates.Reverse().ToArray(), Mix(100), Policy());
        Assert.Equal(first.Alternatives.SelectMany(item => item.CandidateIds),
            second.Alternatives.SelectMany(item => item.CandidateIds));
    }

    [Fact]
    public void SearchPruningIsVisibleAndNeverProvesInventoryImpossible()
    {
        var candidates = Enumerable.Range(0, 520).Select(_ => Candidate("OOH", 10, [], [])).ToArray();
        var result = CampaignCombinationAssessment.Evaluate(candidates, Mix(100), Policy());
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
        var result = CampaignCombinationAssessment.Evaluate([cheap, evidenced, screen], Mix(100), Policy());
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
        var result = CampaignCombinationAssessment.Evaluate([Candidate("OOH", 10, [], []), screen], Mix(100), Policy());
        Assert.Empty(result.Alternatives);
    }

    [Fact]
    public void SupplierCostThatFitsCannotBypassCanonicalClientPriceBudget()
    {
        var result = CampaignCombinationAssessment.Evaluate(
            [Candidate("OOH", 90, [], []), Candidate("DOOH", 50, [], [])],
            Mix(100), Policy(markupBasisPoints: 1_000, vatRateBasisPoints: 1_500));

        Assert.True(result.ClientPriceAssessed);
        Assert.Empty(result.Alternatives);
    }

    private static CommercialPolicyRow Policy(
        int markupBasisPoints = 0,
        int vatRateBasisPoints = 0) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
        markupBasisPoints, 0, 0,
        vatRateBasisPoints > 0 ? MasterDataCodes.VatStatuses.Registered : MasterDataCodes.VatStatuses.NotApplicable,
        vatRateBasisPoints, false, "ZAR", 0, true,
        Guid.NewGuid(), DateTimeOffset.UnixEpoch, 1);

    private static MediaMixVersionView Mix(long channelBudget) => new(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), null, 1, channelBudget * 2, "ZAR", [new("OOH", channelBudget, "Presence", []),
            new("DOOH", channelBudget, "Timed messages", [])], [], "fixture", "APPROVED", Guid.NewGuid(),
        Guid.NewGuid(), 1, DateTimeOffset.UnixEpoch);

    private static InventoryShortlistCandidateView Candidate(string channel, long? cost,
        Guid[] required, Guid[] covered) => new(
        Id: Guid.NewGuid(), InventoryTenantId: Guid.NewGuid(), SupplierId: null, SupplierName: null,
        Latitude: null, Longitude: null, MarketplaceListingVersionId: null,
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
