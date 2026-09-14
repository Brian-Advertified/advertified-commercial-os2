using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Intelligence;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningCommands
{
    private async Task<CommandOutcome> ApproveAudienceStrategyOutcomeAsync(
        Guid audienceArtifactId,
        CommandEnvelope<ApproveAudienceStrategyCommand> envelope,
        CancellationToken cancellationToken)
    {
        var strategy = await store.FindAudienceAsync(
            envelope.TenantId, audienceArtifactId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Audience strategy access denied.");
        var brief = await LoadIntelligenceReadyBriefAsync(
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

        var draft = PlanningRecordStore.BuildAudienceView(strategy);
        var targetIds = envelope.Command.TargetAudienceIds.Distinct().ToArray();
        var definitionIds = draft.Definitions.Select(item => item.Id).ToHashSet();
        if (targetIds.Length == 0 ||
            targetIds.Any(id => !definitionIds.Contains(id)))
        {
            throw new PlanningApprovalBlockedException();
        }

        var selectedTargets = draft.Definitions
            .Where(item => targetIds.Contains(item.Id))
            .ToArray();
        if (selectedTargets.Any(item => !HasUsableAudienceContext(item)))
        {
            throw new PlanningApprovalBlockedException();
        }

        var targetingRationale = OpportunityCommandSupport.Required(
            envelope.Command.TargetingRationale ?? string.Empty, 4000,
            nameof(envelope.Command.TargetingRationale));
        var positioningStatement = OpportunityCommandSupport.Required(
            envelope.Command.PositioningStatement ?? string.Empty, 4000,
            nameof(envelope.Command.PositioningStatement));
        _ = OpportunityCommandSupport.Required(
            envelope.Command.Reason ?? "Audience strategy reviewed and approved.",
            1000, nameof(envelope.Command.Reason));

        var approvedArtifact = new AudienceStrategyArtifact(
            targetIds,
            targetingRationale,
            positioningStatement,
            draft.Definitions.Select(item => new AudienceSegmentArtifact(
                item.Id, item.Name, item.Description, item.NeedState,
                item.BuyingContext, item.Geographies, item.Language, item.LifeStage,
                item.LsmSem, item.LsmSemTaxonomy, item.LsmSemTaxonomyVersion,
                item.Classification, item.Exclusions, item.EvidenceItemIds,
                item.ReferenceObservationIds, item.Confidence, item.LsmSemMandatory)).ToArray());
        var now = timeProvider.GetUtcNow();
        var stored = await IntelligenceArtifactStore.ApproveRevisionAsync(
            store.DbContext,
            envelope.TenantId,
            envelope.ActorId,
            audienceArtifactId,
            envelope.ExpectedVersion,
            Write(approvedArtifact),
            now,
            cancellationToken);
        var approved = await store.FindAudienceAsync(
            envelope.TenantId, stored.Id, cancellationToken)
            ?? throw new InvalidOperationException("The approved Audience Intelligence artifact is unavailable.");
        var view = PlanningRecordStore.BuildAudienceView(approved);
        return OpportunityCommandSupport.Outcome(
            envelope, view, stored.Id, stored.Version,
            MasterDataReferences.CommercialResourceTypes.IntelligenceArtifact,
            MasterDataReferences.CommercialActions.AudienceIntelligenceApproved,
            MasterDataReferences.CommercialEventTypes.AudienceIntelligenceApproved, now);
    }

    private static bool HasUsableAudienceContext(AudienceSegmentView item) =>
        !string.IsNullOrWhiteSpace(item.NeedState) ||
        !string.IsNullOrWhiteSpace(item.BuyingContext) ||
        !string.IsNullOrWhiteSpace(item.Language) ||
        !string.IsNullOrWhiteSpace(item.LifeStage) ||
        !string.IsNullOrWhiteSpace(item.LsmSem) ||
        item.EvidenceItemIds.Count > 0 ||
        item.ReferenceObservationIds.Count > 0;
}
