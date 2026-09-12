using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Infrastructure.CommercialSettings;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class CampaignCombinationAssessment
{
    private const int CandidateLimit = 512;
    private const int BeamWidth = 16;
    private const int RoundLimit = 64;
    private const int AlternativeLimit = 3;

    internal static CampaignCombinationsView Evaluate(
        IReadOnlyList<InventoryShortlistCandidateView> candidates,
        MediaMixVersionView mix,
        CommercialPolicyRow? commercialPolicy)
    {
        var budgets = mix.Allocations.Where(item => item.BudgetMinor > 0)
            .ToDictionary(item => item.Channel, item => item.BudgetMinor, StringComparer.Ordinal);
        var eligible = candidates.Where(item => item.IsEligible && budgets.ContainsKey(item.Channel) &&
            item.Currency == mix.Currency).ToArray();
        var missingCost = eligible.Count(item => !HasCost(item));
        if (budgets.Count == 0 || commercialPolicy is null ||
            !string.Equals(commercialPolicy.Currency, mix.Currency, StringComparison.Ordinal))
            return new([], false, 0, missingCost, false);

        var required = candidates.SelectMany(item => item.SpatialMatch?.RequiredRequirementIds ?? [])
            .Distinct().Order().ToArray();
        var policy = PlanningPolicy.Load().SuitabilityPolicyVersion;
        var priced = eligible.Where(item => HasCost(item) &&
                item.Suitability?.PolicyVersion == policy && FitsCreative(item))
            .OrderByDescending(item => item.Suitability?.BuyAssessment?.IsTargetAudience == true)
            .ThenByDescending(item => item.Score ?? 0m)
            .ThenBy(item => Cost(item)).ThenBy(item => item.Id).ToArray();
        var pool = priced.Take(CandidateLimit).ToArray();
        var search = Search(pool, budgets, required, mix.TotalBudgetMinor, commercialPolicy);
        var alternatives = search.Completed.Take(AlternativeLimit)
            .Select(state => View(state, mix, budgets)).ToArray();
        return new(
            CampaignRelativeComparison.Attach(alternatives, candidates, mix),
            search.Truncated || priced.Length > pool.Length,
            pool.Length,
            missingCost,
            true);
    }

    private static SearchResult Search(
        InventoryShortlistCandidateView[] pool,
        IReadOnlyDictionary<string, long> budgets,
        Guid[] required,
        long totalBudget,
        CommercialPolicyRow commercialPolicy)
    {
        var active = new[] { State.Empty };
        var completed = new List<State>();
        var truncated = false;
        var round = 0;
        while (active.Length > 0 && round++ < RoundLimit)
        {
            var next = new Dictionary<string, State>(StringComparer.Ordinal);
            foreach (var state in active)
            {
                if (Complete(state, budgets.Keys, required)) { completed.Add(state); continue; }
                foreach (var candidate in Options(state, pool, budgets.Keys, required))
                {
                    var extended = Extend(state, candidate, budgets, totalBudget, commercialPolicy);
                    if (extended is not null) next.TryAdd(Key(extended), extended);
                }
            }
            if (next.Count > BeamWidth) truncated = true;
            active = Order(next.Values).Take(BeamWidth).ToArray();
        }
        return new(Order(completed).DistinctBy(Key).ToArray(), truncated || active.Length > 0);
    }

    private static IEnumerable<InventoryShortlistCandidateView> Options(
        State state,
        InventoryShortlistCandidateView[] pool,
        IEnumerable<string> channels,
        Guid[] required)
    {
        var channel = channels.Order(StringComparer.Ordinal)
            .FirstOrDefault(item => !state.ChannelClientPrices.ContainsKey(item));
        if (channel is not null)
            return pool.Where(item => item.Channel == channel && NotDuplicate(state, item));
        var missing = required.First(item => !state.Covered.Contains(item));
        return pool.Where(item =>
            (item.SpatialMatch?.MatchedRequiredRequirementIds.Contains(missing) ?? false) &&
            NotDuplicate(state, item));
    }

    private static State? Extend(
        State state,
        InventoryShortlistCandidateView candidate,
        IReadOnlyDictionary<string, long> budgets,
        long totalBudget,
        CommercialPolicyRow commercialPolicy)
    {
        var supplierCost = Cost(candidate);
        var clientPrice = PlanAmounts.ClientPriceMinor(supplierCost, commercialPolicy);
        var channel = candidate.Channel;
        var priorClient = state.ChannelClientPrices.GetValueOrDefault(channel);
        if (clientPrice > budgets[channel] - priorClient ||
            clientPrice > totalBudget - state.ClientTotal) return null;
        var clientPrices = new Dictionary<string, long>(state.ChannelClientPrices, StringComparer.Ordinal)
        {
            [channel] = checked(priorClient + clientPrice),
        };
        var supplierCosts = new Dictionary<string, long>(state.ChannelSupplierCosts, StringComparer.Ordinal)
        {
            [channel] = checked(state.ChannelSupplierCosts.GetValueOrDefault(channel) + supplierCost),
        };
        return new(
            [.. state.Candidates, candidate],
            supplierCosts,
            clientPrices,
            state.Covered.Concat(candidate.SpatialMatch?.MatchedRequiredRequirementIds ?? []).ToHashSet(),
            checked(state.SupplierTotal + supplierCost),
            checked(state.ClientTotal + clientPrice));
    }

    private static IOrderedEnumerable<State> Order(IEnumerable<State> states) => states
        .OrderByDescending(item => item.Covered.Count)
        .ThenByDescending(item => item.Candidates.Count == 0 ? 0m :
            (decimal)item.Candidates.Count(value => value.Suitability?.BuyAssessment?.IsTargetAudience == true) /
            item.Candidates.Count)
        .ThenByDescending(item => item.Candidates.Count == 0 ? 0m :
            item.Candidates.Average(value => value.Score ?? 0m))
        .ThenBy(item => item.ClientTotal)
        .ThenBy(item => item.SupplierTotal)
        .ThenBy(Key, StringComparer.Ordinal);

    private static CampaignCombinationView View(
        State state,
        MediaMixVersionView mix,
        Dictionary<string, long> budgets) => new(
        state.Candidates.Select(item => item.Id).Order().ToArray(),
        state.SupplierTotal,
        state.ClientTotal,
        mix.Currency,
        state.ChannelClientPrices.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new CampaignChannelCostView(
                item.Key,
                state.ChannelSupplierCosts[item.Key],
                item.Value,
                budgets[item.Key])).ToArray(),
        state.Covered.Order().ToArray(),
        EvidenceGaps(state));

    private static string[] EvidenceGaps(State state)
    {
        var gaps = new List<string> { "campaignCombination.uniqueReachAndDuplication" };
        if (state.Candidates.Any(RequiresHumanReview))
            gaps.Add("campaignCombination.humanReviewRequired");
        return gaps.ToArray();
    }

    private static bool RequiresHumanReview(InventoryShortlistCandidateView candidate) =>
        candidate.Suitability?.EvidenceGaps.Count > 0 ||
        candidate.CommercialReadiness?.EvidenceGaps.Count > 0;

    private static bool Complete(State state, IEnumerable<string> channels, Guid[] required) =>
        channels.All(state.ChannelClientPrices.ContainsKey) &&
        required.All(state.Covered.Contains) &&
        state.Candidates.Count > 0;

    private static bool NotDuplicate(State state, InventoryShortlistCandidateView item) =>
        state.Candidates.All(existing =>
            existing.InventoryTenantId != item.InventoryTenantId ||
            existing.InventoryProductId != item.InventoryProductId);

    private static bool HasCost(InventoryShortlistCandidateView candidate) =>
        candidate.Suitability?.BuyAssessment?.CampaignSupplierCostMinor is >= 0;

    private static bool FitsCreative(InventoryShortlistCandidateView candidate)
    {
        var digital = candidate.Suitability?.BuyAssessment?.DigitalExposure;
        return !(digital?.SpotLengthSeconds > digital?.SlotLengthSeconds);
    }

    private static long Cost(InventoryShortlistCandidateView candidate) =>
        candidate.Suitability!.BuyAssessment!.CampaignSupplierCostMinor!.Value;

    private static string Key(State state) =>
        string.Join(',', state.Candidates.Select(item => item.Id).Order());

    private sealed record State(
        IReadOnlyList<InventoryShortlistCandidateView> Candidates,
        Dictionary<string, long> ChannelSupplierCosts,
        Dictionary<string, long> ChannelClientPrices,
        HashSet<Guid> Covered,
        long SupplierTotal,
        long ClientTotal)
    {
        internal static State Empty => new(
            [], new(StringComparer.Ordinal), new(StringComparer.Ordinal), [], 0, 0);
    }

    private sealed record SearchResult(IReadOnlyList<State> Completed, bool Truncated);
}
