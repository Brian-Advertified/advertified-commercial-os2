using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningCommands
{
    private async Task<CommandOutcome> ApproveAudienceStrategyOutcomeAsync(
        Guid audienceSetId,
        CommandEnvelope<ApproveAudienceStrategyCommand> envelope,
        CancellationToken cancellationToken)
    {
        var strategy = await store.FindAudienceAsync(
            envelope.TenantId, audienceSetId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Audience strategy access denied.");
        var brief = await LoadPlanningReadyBriefAsync(
            strategy.BriefVersionId, envelope, cancellationToken);
        var latest = await store.FindLatestAudienceAsync(
            envelope.TenantId, strategy.BriefVersionId, cancellationToken);
        if (strategy.Status != MasterDataCodes.LifecycleStatuses.Draft ||
            latest?.Id != strategy.Id ||
            strategy.InputHash != PlanningHash.ForBrief(brief))
        {
            throw new InvalidLifecycleTransitionException();
        }
        if (strategy.Version != envelope.ExpectedVersion)
        {
            throw new VersionConflictException();
        }

        var draft = await store.BuildAudienceViewAsync(
            envelope.TenantId, strategy, cancellationToken);
        var targetIds = envelope.Command.TargetAudienceIds.Distinct().ToArray();
        var definitionIds = draft.Definitions.Select(item => item.Id).ToHashSet();
        if (targetIds.Length == 0 ||
            targetIds.Any(id => !definitionIds.Contains(id)))
        {
            throw new PlanningApprovalBlockedException();
        }

        var targetingRationale = OpportunityCommandSupport.Required(
            envelope.Command.TargetingRationale, 4000,
            nameof(envelope.Command.TargetingRationale));
        var positioningStatement = OpportunityCommandSupport.Required(
            envelope.Command.PositioningStatement, 4000,
            nameof(envelope.Command.PositioningStatement));
        _ = OpportunityCommandSupport.Required(
            envelope.Command.Reason ?? "Audience strategy reviewed and approved.",
            1000, nameof(envelope.Command.Reason));

        var approvedSetId = Guid.NewGuid();
        var definitionMap = draft.Definitions.ToDictionary(
            item => item.Id, _ => Guid.NewGuid());
        var approvedTargetIds = targetIds.Select(id => definitionMap[id]).ToArray();
        var approvedRecords = draft.Definitions.Select(item =>
            new PlannedAudienceRecord(
                definitionMap[item.Id],
                ToApprovedProposal(item, targetIds.Contains(item.Id)))).ToArray();
        var now = timeProvider.GetUtcNow();
        var approvedTargetIdsJson = Write(approvedTargetIds);
        var inserted = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.audience_definition_sets (
                id, tenant_id, brief_version_id, version_no,
                target_audience_ids_json, targeting_rationale,
                positioning_statement, input_hash, agent_provider_code,
                agent_model_code, agent_incremental_cost_minor,
                agent_provider_request_id, status_code, created_by,
                approved_by, approved_at_utc, version, created_at_utc)
            SELECT {approvedSetId}, tenant_id, brief_version_id, version_no + 1,
                {approvedTargetIdsJson}::jsonb, {targetingRationale},
                {positioningStatement}, input_hash, agent_provider_code,
                agent_model_code, agent_incremental_cost_minor,
                agent_provider_request_id, {MasterDataCodes.LifecycleStatuses.Approved},
                created_by, {envelope.ActorId.Value}, {now}, version + 1, {now}
            FROM commercial.audience_definition_sets
            WHERE tenant_id = {envelope.TenantId.Value}
              AND id = {audienceSetId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Draft}
            """, cancellationToken);
        if (inserted != 1)
        {
            throw new VersionConflictException();
        }

        await PlanningAudiencePersistence.InsertAsync(
            store.DbContext, envelope.TenantId, approvedSetId, approvedRecords,
            MasterDataCodes.LifecycleStatuses.Approved, cancellationToken);
        var approved = await store.FindAudienceAsync(
            envelope.TenantId, approvedSetId, cancellationToken)
            ?? throw new InvalidOperationException("The approved audience strategy is unavailable.");
        var view = await store.BuildAudienceViewAsync(
            envelope.TenantId, approved, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, approvedSetId, approved.Version,
            MasterDataReferences.CommercialResourceTypes.AudienceDefinitionSet,
            MasterDataReferences.CommercialActions.StrategyApproved,
            MasterDataReferences.CommercialEventTypes.StrategyApproved, now);
    }

    private static AudienceDefinitionProposal ToApprovedProposal(
        AudienceDefinitionView item,
        bool isTarget) => new(
            item.Name,
            item.Description,
            item.NeedState,
            item.BuyingContext,
            item.Geographies,
            item.Language,
            item.LifeStage,
            item.LsmSem,
            item.LsmSemTaxonomy,
            item.LsmSemTaxonomyVersion,
            item.Classification,
            item.Exclusions,
            item.EvidenceItemIds,
            item.Confidence,
            isTarget,
            item.LsmSemMandatory);
}
