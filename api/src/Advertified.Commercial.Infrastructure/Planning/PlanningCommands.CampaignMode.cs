using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningCommands
{
    private async Task<CommandOutcome> SelectCampaignModeOutcomeAsync(
        Guid briefVersionId,
        CommandEnvelope<SelectCampaignModeCommand> envelope,
        CancellationToken cancellationToken)
    {
        await LoadApprovedBriefAsync(briefVersionId, envelope, cancellationToken);
        var existing = await store.FindCampaignModeAsync(
            envelope.TenantId, briefVersionId, cancellationToken);
        if (existing is not null || await store.HasPlanningArtifactsAsync(
                envelope.TenantId, briefVersionId, cancellationToken))
        {
            throw new CampaignModeLockedException();
        }

        var mode = campaignModePolicy.Require(envelope.Command.Mode);
        if (!mode.ImmutableAfterSelection)
        {
            throw new InvalidOperationException(
                "The selected campaign mode is not configured as immutable.");
        }
        var decisionSource = envelope.Command.DecisionSource.Trim().ToUpperInvariant();
        await OpportunityCommandSupport.EnsureCodeAsync(
            store.DbContext,
            MasterDataCodes.CampaignModeDecisionSources.Collection,
            decisionSource,
            cancellationToken);
        if (envelope.Command.Confidence is < 0 or > 1)
        {
            throw new ArgumentException("Campaign mode confidence must be between zero and one.");
        }
        var reason = string.IsNullOrWhiteSpace(envelope.Command.Reason)
            ? null
            : envelope.Command.Reason.Trim();
        if (reason is { Length: > 2000 })
        {
            throw new ArgumentException("The campaign mode reason is too long.");
        }

        var id = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.campaign_mode_selections (
                id, tenant_id, brief_version_id, mode_code, decision_source_code,
                confidence, reason, selected_by, version, selected_at_utc)
            VALUES ({id}, {envelope.TenantId.Value}, {briefVersionId}, {mode.Code},
                {decisionSource}, {envelope.Command.Confidence}, {reason},
                {envelope.ActorId.Value}, 1, {now})
            """, cancellationToken);
        var row = await store.FindCampaignModeAsync(
            envelope.TenantId, briefVersionId, cancellationToken)
            ?? throw new InvalidOperationException("The campaign mode was not persisted.");
        var view = PlanningRecordStore.BuildCampaignModeView(row, campaignModePolicy);
        return OpportunityCommandSupport.Outcome(
            envelope,
            view,
            id,
            row.Version,
            MasterDataReferences.CommercialResourceTypes.CampaignModeSelection,
            MasterDataReferences.CommercialActions.CampaignModeSelected,
            MasterDataReferences.CommercialEventTypes.CampaignModeSelected,
            now);
    }

    private async Task<CampaignModeRow> RequireCampaignModeAsync(
        TenantId tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken) =>
        await store.FindCampaignModeAsync(tenantId, briefVersionId, cancellationToken)
        ?? throw new CampaignModeRequiredException();
}
