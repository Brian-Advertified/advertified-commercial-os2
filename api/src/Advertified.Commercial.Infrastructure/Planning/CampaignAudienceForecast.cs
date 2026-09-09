using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class CampaignAudienceForecast
{
    private const string MissingReach = "campaignAudience.missingReach";
    private const string MissingImpressions = "campaignAudience.missingImpressions";
    private const string IncompatibleBasis = "campaignAudience.incompatibleMeasurementBasis";
    private const string MissingDeduplication = "campaignAudience.deduplicatedReachUnavailable";
    private const string CrossTenantPortfolio = "campaignAudience.crossTenantPortfolioUnavailable";
    private const string MissingIncremental = "campaignAudience.incrementalReachUnavailable";

    internal static CampaignCombinationView[] Attach(
        IReadOnlyList<CampaignCombinationView> alternatives,
        IReadOnlyList<InventoryShortlistCandidateView> candidates,
        IReadOnlyList<CampaignResearchPortfolioEvidence> portfolios,
        TenantId planningTenantId)
    {
        var byId = candidates.ToDictionary(item => item.Id);
        return alternatives.Select(item => Attach(item, byId, portfolios, planningTenantId)).ToArray();
    }

    private static CampaignCombinationView Attach(
        CampaignCombinationView combination,
        Dictionary<Guid, InventoryShortlistCandidateView> candidates,
        IReadOnlyList<CampaignResearchPortfolioEvidence> portfolios,
        TenantId planningTenantId)
    {
        var selected = combination.CandidateIds.Select(id => candidates[id]).ToArray();
        var forecast = Evaluate(selected, portfolios, planningTenantId);
        var gaps = combination.EvidenceGaps
            .Where(item => item != "campaignCombination.uniqueReachAndDuplication")
            .Concat(forecast.EvidenceGaps).Distinct(StringComparer.Ordinal).ToArray();
        return combination with { AudienceForecast = forecast, EvidenceGaps = gaps };
    }

    internal static CampaignAudienceForecastView Evaluate(
        IReadOnlyList<InventoryShortlistCandidateView> selected,
        IReadOnlyList<CampaignResearchPortfolioEvidence> portfolios,
        TenantId planningTenantId)
    {
        if (selected.Count == 0)
            return Empty([MissingReach, MissingImpressions, MissingDeduplication]);
        var assessments = selected.Select(item => item.Suitability?.BuyAssessment).ToArray();
        var gaps = new List<string>();
        var basis = CompatibleBasis(assessments);
        if (basis is null) gaps.Add(IncompatibleBasis);
        var grossReach = basis is null ? null :
            SumWhenComplete(assessments.Select(item => item?.Reach), MissingReach, gaps);
        var impressions = basis is null ? null :
            SumWhenComplete(assessments.Select(item => item?.Impressions), MissingImpressions, gaps);
        if (selected.Any(item => item.InventoryTenantId != planningTenantId.Value))
            gaps.Add(CrossTenantPortfolio);
        var productVersions = selected.Select(item => item.ProductVersionId).Order().ToArray();
        var portfolio = basis is null ? null : ExactPortfolio(portfolios, productVersions, basis);
        if (portfolio is null) gaps.Add(MissingDeduplication);
        var deduplicated = portfolio?.DeduplicatedReach;
        if (grossReach.HasValue && deduplicated > grossReach.Value)
        {
            deduplicated = null;
            gaps.Add(IncompatibleBasis);
        }
        decimal? duplicated = grossReach.HasValue && deduplicated.HasValue
            ? grossReach.Value - deduplicated.Value : null;
        decimal? frequency = impressions.HasValue && deduplicated is > 0
            ? impressions.Value / deduplicated.Value : null;
        var increments = Incremental(selected, portfolios, basis, deduplicated, gaps);
        return new(grossReach, deduplicated, duplicated, impressions, frequency,
            basis?.Universe, basis?.Period, basis?.Source, basis?.Methodology,
            increments, gaps.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static CampaignCandidateIncrementalReachView[] Incremental(
        IReadOnlyList<InventoryShortlistCandidateView> selected,
        IReadOnlyList<CampaignResearchPortfolioEvidence> portfolios,
        MeasurementBasis? basis,
        decimal? fullReach,
        List<string> gaps)
    {
        if (!fullReach.HasValue || basis is null)
            return selected.Select(item => new CampaignCandidateIncrementalReachView(
                item.Id, null, MissingIncremental)).ToArray();
        var result = selected.Select(candidate =>
        {
            var remainder = selected.Where(item => item.Id != candidate.Id).ToArray();
            var without = DeduplicatedReach(remainder, portfolios, basis);
            return without.HasValue
                ? new CampaignCandidateIncrementalReachView(candidate.Id,
                    Math.Max(0, fullReach.Value - without.Value), null)
                : new CampaignCandidateIncrementalReachView(candidate.Id, null, MissingIncremental);
        }).ToArray();
        if (result.Any(item => !item.IncrementalReach.HasValue)) gaps.Add(MissingIncremental);
        return result;
    }

    private static decimal? DeduplicatedReach(
        InventoryShortlistCandidateView[] selected,
        IReadOnlyList<CampaignResearchPortfolioEvidence> portfolios,
        MeasurementBasis basis)
    {
        if (selected.Length == 0) return 0;
        if (selected.Length == 1)
        {
            var assessment = selected[0].Suitability?.BuyAssessment;
            return SameBasis(assessment, basis) ? assessment?.Reach : null;
        }
        var ids = selected.Select(item => item.ProductVersionId).Order().ToArray();
        return ExactPortfolio(portfolios, ids, basis)?.DeduplicatedReach;
    }

    private static CampaignResearchPortfolioEvidence? ExactPortfolio(
        IReadOnlyList<CampaignResearchPortfolioEvidence> portfolios,
        IReadOnlyList<Guid> productVersions,
        MeasurementBasis basis)
    {
        var matches = portfolios.Where(item =>
            item.Unit == MasterDataCodes.MeasurementUnits.People &&
            item.ProductVersionIds.SequenceEqual(productVersions) && SameBasis(item, basis)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static MeasurementBasis? CompatibleBasis(InventoryBuyAssessmentView?[] assessments)
    {
        if (assessments.Any(item => item is null || !CompleteBasis(item))) return null;
        var first = assessments[0]!;
        var basis = new MeasurementBasis(first.Universe!, first.MeasurementPeriod!,
            first.MeasurementSource!, first.Methodology!);
        return assessments.All(item => SameBasis(item, basis)) ? basis : null;
    }

    private static bool CompleteBasis(InventoryBuyAssessmentView item) =>
        !string.IsNullOrWhiteSpace(item.Universe) && !string.IsNullOrWhiteSpace(item.MeasurementPeriod) &&
        !string.IsNullOrWhiteSpace(item.MeasurementSource) && !string.IsNullOrWhiteSpace(item.Methodology);

    private static bool SameBasis(InventoryBuyAssessmentView? item, MeasurementBasis basis) =>
        item is not null && item.Universe == basis.Universe && item.MeasurementPeriod == basis.Period &&
        item.MeasurementSource == basis.Source && item.Methodology == basis.Methodology;

    private static bool SameBasis(CampaignResearchPortfolioEvidence item, MeasurementBasis basis) =>
        item.Universe == basis.Universe && item.MeasurementPeriod == basis.Period &&
        item.MeasurementSource == basis.Source && item.Methodology == basis.Methodology;

    private static decimal? SumWhenComplete(IEnumerable<decimal?> values, string gap, List<string> gaps)
    {
        var items = values.ToArray();
        if (items.Any(item => !item.HasValue)) { gaps.Add(gap); return null; }
        return items.Sum(item => item!.Value);
    }

    private static CampaignAudienceForecastView Empty(string[] gaps) =>
        new(null, null, null, null, null, null, null, null, null, [], gaps);

    private sealed record MeasurementBasis(string Universe, string Period, string Source, string Methodology);
}
