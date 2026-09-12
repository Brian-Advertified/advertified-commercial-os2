using System.Text.Json;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Planning;

namespace Advertified.Commercial.Infrastructure.Proposal;

internal static class ProposalCampaignContextBuilder
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    internal static async Task<ProposalCampaignContextView?> BuildAsync(
        PlanningRecordStore planningStore,
        TenantId tenantId,
        ProposalVersionView proposal,
        CancellationToken cancellationToken)
    {
        var brief = await planningStore.FindBriefAsync(
            tenantId, proposal.BriefVersionId, cancellationToken);
        if (brief is null) return null;

        var audienceArtifactIds = new HashSet<Guid>();
        var evaluated = new HashSet<(Guid InventoryTenantId, Guid ProductVersionId)>();
        var eligible = new HashSet<(Guid InventoryTenantId, Guid ProductVersionId)>();
        var selected = new HashSet<(Guid InventoryTenantId, Guid ProductVersionId)>();
        var suppliers = new HashSet<Guid>();
        foreach (var planId in proposal.Options.Select(item => item.PlanVersionId).Distinct())
        {
            var plan = await planningStore.FindPlanAsync(tenantId, planId, cancellationToken);
            if (plan is null) continue;
            var mix = await planningStore.FindMixAsync(tenantId, plan.MixVersionId, cancellationToken);
            if (mix is not null) audienceArtifactIds.Add(mix.AudienceArtifactId);
            var shortlist = await planningStore.FindShortlistAsync(
                tenantId, plan.ShortlistVersionId, cancellationToken);
            if (shortlist is null) continue;
            var shortlistView = await planningStore.BuildShortlistViewAsync(
                tenantId, shortlist, cancellationToken);
            foreach (var candidate in shortlistView.Candidates)
            {
                var key = (candidate.InventoryTenantId, candidate.ProductVersionId);
                evaluated.Add(key);
                if (candidate.SupplierId.HasValue) suppliers.Add(candidate.SupplierId.Value);
                if (candidate.IsEligible) eligible.Add(key);
                if (candidate.IsSelected is true) selected.Add(key);
            }
        }

        var consistent = audienceArtifactIds.Count == 1;
        var names = Array.Empty<string>();
        string? rationale = null;
        string? positioning = null;
        if (consistent)
        {
            var audience = await planningStore.FindAudienceAsync(
                tenantId, audienceArtifactIds.Single(), cancellationToken);
            if (audience is not null)
            {
                var view = PlanningRecordStore.BuildAudienceView(audience);
                var targets = new HashSet<Guid>(view.TargetAudienceIds);
                names = view.Definitions.Where(item => targets.Contains(item.Id))
                    .Select(item => item.Name).ToArray();
                rationale = view.TargetingRationale;
                positioning = view.PositioningStatement;
            }
        }

        return new ProposalCampaignContextView(
            brief.BusinessProblem,
            brief.Objective,
            names,
            rationale,
            positioning,
            ReadList(brief.GeographiesJson),
            ReadList(brief.MeasurementJson),
            consistent,
            evaluated.Count,
            eligible.Count,
            suppliers.Count,
            selected.Count);
    }

    private static string[] ReadList(string json) =>
        JsonSerializer.Deserialize<string[]>(json, StoredJson) ?? [];
}
