using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventorySuitabilityScorerTests
{
    [Fact]
    public void PreviousPolicyRemainsAddressableButNewScreeningUsesDistinctIdentity()
    {
        var policies = MasterDataRegistryReader.Read().Collections
            .Single(item => item.Code == "planningPolicies").Items;
        var previous = policies.Single(item => item.Code == "INVENTORY_SUITABILITY_OOH_V1");
        var current = policies.Single(item => item.Code == PlanningPolicy.Load().SuitabilityPolicyVersion);

        Assert.False(previous.IsActive);
        Assert.True(current.IsActive);
        Assert.NotEqual(previous.Code, current.Code);
        var candidate = Candidate(100);
        var historical = candidate with { Suitability = candidate.Suitability with { PolicyVersion = previous.Code } };
        var rescored = Assert.Single(InventorySuitabilityScorer.Score([historical], PlanningPolicy.Load()));
        Assert.Equal(previous.Code, historical.Suitability.PolicyVersion);
        Assert.Equal(current.Code, rescored.Suitability.PolicyVersion);
    }

    [Fact]
    public void CheapUnitRateAndAllocationDoNotEstablishStrategicValue()
    {
        var policy = PlanningPolicy.Load();
        Assert.Equal("INVENTORY_SUITABILITY_OOH_V2", policy.SuitabilityPolicyVersion);
        var cheap = Candidate(100);
        var expensive = Candidate(1_000_000);
        var scored = InventorySuitabilityScorer.Score([cheap, expensive], policy);

        Assert.Equal(scored[0].Suitability.Total, scored[1].Suitability.Total);
        foreach (var item in scored)
        {
            Assert.True(item.Eligibility.IsEligible);
            Assert.Equal(0m, item.Suitability.ObjectiveFormat);
            Assert.Equal(0m, item.Suitability.BudgetEfficiency);
            Assert.Equal(0m, item.Suitability.PortfolioCoverageDiversity);
            Assert.Contains("suitability.objectiveFormatEvidence", item.Suitability.EvidenceGaps);
            Assert.Contains("suitability.comparableTargetExposureCost", item.Suitability.EvidenceGaps);
            Assert.Contains("suitability.incrementalReachEvidence", item.Suitability.EvidenceGaps);
        }
    }

    [Fact]
    public void AddingCataloguePeersDoesNotReduceCampaignContribution()
    {
        var policy = PlanningPolicy.Load();
        var candidate = Candidate(100);
        var alone = Assert.Single(InventorySuitabilityScorer.Score([candidate], policy));
        var amongPeers = InventorySuitabilityScorer.Score([candidate, Candidate(100)], policy)[0];

        Assert.Equal(alone.Suitability.Total, amongPeers.Suitability.Total);
        Assert.Equal(policy.SuitabilityWeights.Geography + policy.SuitabilityWeights.AudienceContext +
            policy.SuitabilityWeights.EvidenceQualityFreshness, alone.Suitability.Total);
    }

    [Fact]
    public void ScoringPreservesHardRejection()
    {
        var candidate = Candidate(100) with
        {
            Eligibility = new(false, "INELIGIBLE_GEOGRAPHY", "Outside the required area.", null),
        };
        var scored = Assert.Single(InventorySuitabilityScorer.Score([candidate], PlanningPolicy.Load()));

        Assert.Equal(candidate.Eligibility, scored.Eligibility);
        Assert.Equal(0m, scored.Suitability.Total);
    }

    private static PreparedShortlistCandidate Candidate(long rate)
    {
        var inventory = new PlanningInventoryRow(
            InventoryTenantId: Guid.NewGuid(), MarketplaceListingVersionId: null,
            ProductId: Guid.NewGuid(), ProductVersionId: Guid.NewGuid(), SupplierId: Guid.NewGuid(),
            SupplierName: "Verified supplier", Name: "Verified screen", Channel: "DOOH", ProductType: "DOOH_SCREEN", Geography: "Sandton",
            Latitude: null, Longitude: null, RateId: Guid.NewGuid(), RateType: "MONTH_RATE",
            Currency: "ZAR", RateAmountMinor: rate, EffectiveFrom: new(2026, 9, 1),
            EffectiveTo: new(2026, 10, 31), RateSource: "fixture:rate", AvailabilityId: Guid.NewGuid(),
            Availability: "AVAILABLE", ObservedAtUtc: null, ValidUntilUtc: null,
            AvailabilitySource: "fixture:availability", UnavailablePeriodsJson: "[]",
            AudienceProfileJson: null, SupplierVatStatus: "REGISTERED", SupplierCommercialJson: null,
            VatTreatment: "EXCLUSIVE", CommercialTermsJson: null, DeliverableJson: null,
            SpatialJson: null, LogoAssetId: null);
        return new(Guid.NewGuid(), inventory, new("DOOH", 500_000_000, "Relevant reach", []),
            new(true, null, null, null), new(1m, 1m, 1m, []),
            new(false, [], [], [], [], [], [], 1m, []),
            InventorySuitabilityScorer.Empty(PlanningPolicy.Load()), "fixture", "fixture", null);
    }
}
