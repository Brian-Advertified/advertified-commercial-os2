using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class CampaignAudienceForecastTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void CompatiblePortfolioProducesDeduplicatedReachFrequencyAndIncrementality()
    {
        var first = Candidate(120, 300);
        var second = Candidate(100, 240);
        var full = Portfolio([first.ProductVersionId, second.ProductVersionId], 180);
        var result = CampaignAudienceForecast.Evaluate(
            [first, second], [full], new TenantId(TenantId));

        Assert.Equal(220, result.GrossReach);
        Assert.Equal(180, result.DeduplicatedReach);
        Assert.Equal(40, result.DuplicatedReach);
        Assert.Equal(540, result.TotalImpressions);
        Assert.Equal(3, result.AverageFrequency);
        Assert.DoesNotContain("campaignAudience.deduplicatedReachUnavailable", result.EvidenceGaps);
        Assert.Equal(80, result.IncrementalReach.Single(item => item.CandidateId == first.Id).IncrementalReach);
        Assert.Equal(60, result.IncrementalReach.Single(item => item.CandidateId == second.Id).IncrementalReach);
    }

    [Fact]
    public void CompatiblePlacementsWithoutPortfolioNeverInventUniqueReach()
    {
        var first = Candidate(120, 300);
        var second = Candidate(100, 240);
        var result = CampaignAudienceForecast.Evaluate(
            [first, second], [], new TenantId(TenantId));

        Assert.Equal(220, result.GrossReach);
        Assert.Null(result.DeduplicatedReach);
        Assert.Null(result.DuplicatedReach);
        Assert.Null(result.AverageFrequency);
        Assert.All(result.IncrementalReach, item => Assert.Null(item.IncrementalReach));
        Assert.Contains("campaignAudience.deduplicatedReachUnavailable", result.EvidenceGaps);
    }

    [Fact]
    public void IncompatibleMeasurementBasisBlocksCampaignRollup()
    {
        var first = Candidate(120, 300);
        var second = Candidate(100, 240, period: "2026 Q2");
        var result = CampaignAudienceForecast.Evaluate(
            [first, second], [], new TenantId(TenantId));

        Assert.Null(result.DeduplicatedReach);
        Assert.Contains("campaignAudience.incompatibleMeasurementBasis", result.EvidenceGaps);
    }

    private static InventoryShortlistCandidateView Candidate(
        decimal reach, decimal impressions, string period = "2026 Q1")
    {
        var assessment = new InventoryBuyAssessmentView(
            100, "ZAR", reach, impressions, impressions / reach, null, null,
            "Adults 18+", period, "Authorised Research", "Panel methodology",
            true, null, []);
        return new(Guid.NewGuid(), TenantId, null, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "Measured placement", "OOH", "Johannesburg",
            100, "ZAR", true, null, null, .8m, new(null, null, null, []), null, null, null,
            Suitability: InventorySuitabilityScorer.Empty(PlanningPolicy.Load()) with
            { BuyAssessment = assessment });
    }

    private static CampaignResearchPortfolioEvidence Portfolio(Guid[] productVersions, decimal reach) =>
        new(Guid.NewGuid(), productVersions.Order().ToArray(), reach, "PEOPLE",
            "Adults 18+", "2026 Q1", "Authorised Research", "Panel methodology");
}
