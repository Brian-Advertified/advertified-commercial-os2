using Advertified.Commercial.Application.Planning;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class CampaignCombinationAssessment
{
    // Technical search bounds prevent catalogue size from causing unbounded response work.
    private const int CandidateLimit = 512;
    private const int BeamWidth = 16;
    private const int RoundLimit = 64;
    private const int AlternativeLimit = 3;

    internal static CampaignCombinationsView Evaluate(IReadOnlyList<InventoryShortlistCandidateView> candidates,
        MediaMixVersionView mix)
    {
        var budgets = mix.Allocations.Where(item => item.BudgetMinor > 0)
            .ToDictionary(item => item.Channel, item => item.BudgetMinor, StringComparer.Ordinal);
        if (budgets.Count == 0) return new([], false, 0, 0);
        var required = candidates.SelectMany(item => item.SpatialMatch?.RequiredRequirementIds ?? [])
            .Distinct().Order().ToArray();
        var eligible = candidates.Where(item => item.IsEligible && budgets.ContainsKey(item.Channel) &&
            item.Currency == mix.Currency).ToArray();
        var policy = PlanningPolicy.Load().SuitabilityPolicyVersion;
        var priced = eligible.Where(item => HasCost(item) && item.Suitability?.PolicyVersion == policy)
            .OrderByDescending(item => item.Score ?? 0m)
            .ThenBy(item => Cost(item)).ThenBy(item => item.Id).ToArray();
        var pool = priced.Take(CandidateLimit).ToArray();
        var search = Search(pool, budgets, required, mix.TotalBudgetMinor);
        return new(search.Completed.Take(AlternativeLimit).Select(state => View(state, mix, budgets)).ToArray(),
            search.Truncated || priced.Length > pool.Length, pool.Length, eligible.Count(item => !HasCost(item)));
    }

    private static SearchResult Search(InventoryShortlistCandidateView[] pool,
        IReadOnlyDictionary<string, long> budgets, Guid[] required, long totalBudget)
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
                    var extended = Extend(state, candidate, budgets, totalBudget);
                    if (extended is not null) next.TryAdd(Key(extended), extended);
                }
            }
            if (next.Count > BeamWidth) truncated = true;
            active = Order(next.Values).Take(BeamWidth).ToArray();
        }
        return new(Order(completed).DistinctBy(Key).ToArray(), truncated || active.Length > 0);
    }

    private static IEnumerable<InventoryShortlistCandidateView> Options(State state,
        InventoryShortlistCandidateView[] pool, IEnumerable<string> channels, Guid[] required)
    {
        var channel = channels.Order(StringComparer.Ordinal).FirstOrDefault(item => !state.ChannelCosts.ContainsKey(item));
        if (channel is not null) return pool.Where(item => item.Channel == channel && NotDuplicate(state, item));
        var missing = required.First(item => !state.Covered.Contains(item));
        return pool.Where(item => (item.SpatialMatch?.MatchedRequiredRequirementIds.Contains(missing) ?? false) &&
            NotDuplicate(state, item));
    }

    private static bool NotDuplicate(State state, InventoryShortlistCandidateView item) =>
        state.Candidates.All(existing => existing.InventoryTenantId != item.InventoryTenantId ||
            existing.InventoryProductId != item.InventoryProductId);

    private static State? Extend(State state, InventoryShortlistCandidateView candidate,
        IReadOnlyDictionary<string, long> budgets, long totalBudget)
    {
        var cost = Cost(candidate);
        var previousChannelCost = state.ChannelCosts.GetValueOrDefault(candidate.Channel);
        // Subtraction avoids overflow while applying both channel and total supplier-cost ceilings.
        if (cost > budgets[candidate.Channel] - previousChannelCost || cost > totalBudget - state.Total) return null;
        var costs = new Dictionary<string, long>(state.ChannelCosts, StringComparer.Ordinal)
        {
            [candidate.Channel] = previousChannelCost + cost,
        };
        return new([.. state.Candidates, candidate], costs,
            state.Covered.Concat(candidate.SpatialMatch?.MatchedRequiredRequirementIds ?? []).ToHashSet(),
            state.Total + cost);
    }

    private static IOrderedEnumerable<State> Order(IEnumerable<State> states) => states
        .OrderByDescending(item => item.Covered.Count)
        .ThenByDescending(item => item.Candidates.Count == 0 ? 0m : item.Candidates.Average(value => value.Score ?? 0m))
        .ThenBy(item => item.Total).ThenBy(Key, StringComparer.Ordinal);

    private static bool Complete(State state, IEnumerable<string> channels, Guid[] required) =>
        channels.All(state.ChannelCosts.ContainsKey) && required.All(state.Covered.Contains) && state.Candidates.Count > 0;

    private static CampaignCombinationView View(State state, MediaMixVersionView mix,
        Dictionary<string, long> budgets) => new(
        state.Candidates.Select(item => item.Id).Order().ToArray(), state.Total, mix.Currency,
        state.ChannelCosts.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new CampaignChannelCostView(item.Key, item.Value, budgets[item.Key])).ToArray(),
        state.Covered.Order().ToArray(), ["campaignCombination.clientPriceNotAssessed",
            "campaignCombination.uniqueReachAndDuplication", "campaignCombination.humanReviewRequired"]);

    private static bool HasCost(InventoryShortlistCandidateView candidate) =>
        candidate.Suitability?.BuyAssessment?.CampaignSupplierCostMinor is >= 0;

    private static long Cost(InventoryShortlistCandidateView candidate) =>
        candidate.Suitability!.BuyAssessment!.CampaignSupplierCostMinor!.Value;

    private static string Key(State state) => string.Join(',', state.Candidates.Select(item => item.Id).Order());

    private sealed record State(IReadOnlyList<InventoryShortlistCandidateView> Candidates,
        Dictionary<string, long> ChannelCosts, HashSet<Guid> Covered, long Total)
    {
        internal static State Empty => new([], new(StringComparer.Ordinal), [], 0);
    }

    private sealed record SearchResult(IReadOnlyList<State> Completed, bool Truncated);
}
