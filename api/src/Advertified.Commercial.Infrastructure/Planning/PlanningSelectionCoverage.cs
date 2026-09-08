using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Planning;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class PlanningSelectionCoverage
{
    internal static void EnsureChannels(
        IEnumerable<string> selectedChannels,
        IEnumerable<string> requiredChannels)
    {
        var selected = selectedChannels.ToHashSet(StringComparer.Ordinal);
        var required = requiredChannels.ToHashSet(StringComparer.Ordinal);
        if (!required.IsSubsetOf(selected))
        {
            throw new InvalidLifecycleTransitionException();
        }
    }

    internal static void EnsureSpatial(
        IEnumerable<InventoryShortlistCandidateView> selected)
    {
        var candidates = selected.ToArray();
        var required = candidates.SelectMany(item =>
                item.SpatialMatch?.RequiredRequirementIds ?? [])
            .ToHashSet();
        var covered = candidates.SelectMany(item =>
                item.SpatialMatch?.MatchedRequiredRequirementIds ?? [])
            .ToHashSet();
        if (!required.IsSubsetOf(covered))
        {
            throw new InvalidLifecycleTransitionException();
        }
    }
}
