using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Intelligence;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningCommands
{
    private async Task<CommandOutcome> GenerateAudiencesOutcomeAsync(
        Guid briefVersionId,
        CommandEnvelope<GenerateAudiencesCommand> envelope,
        CancellationToken cancellationToken)
    {
        var brief = await LoadIntelligenceReadyBriefAsync(
            briefVersionId, envelope, cancellationToken);
        var problem = BuildCommercialProblemInput(brief, envelope);
        var evidence = await AudienceEvidenceReader.ReadAsync(
            store.DbContext, problem, cancellationToken);
        var referenceEvidence = await ReferenceObservationReader.ReadAsync(
            store.DbContext,
            problem.Geographies,
            "AUDIENCE",
            ["AUDIENCE_SEGMENT_SUPPORT", "AGGREGATE_PLANNING_ONLY", "SENSITIVE_CONTEXT_ONLY"],
            600,
            cancellationToken);
        var proposal = await audienceIntelligenceAgent.ProposeAudiencesAsync(
            new AudienceIntelligenceInput(problem, evidence, referenceEvidence), cancellationToken);
        if (proposal.Usage.IncrementalCostMinor < 0)
            throw new InvalidOperationException("The audience proposal is invalid.");
        AudienceIntelligenceValidator.Validate(
            proposal.Audiences,
            Read<string[]>(brief.GeographiesJson),
            Read<Guid[]>(brief.EvidenceIdsJson),
            evidence,
            referenceEvidence,
            problem.Audiences);
        AudienceEvidenceGuard.Validate(proposal.Audiences, evidence);
        var targetingRationale = OpportunityCommandSupport.Optional(
            proposal.TargetingRationale, 4000, nameof(proposal.TargetingRationale));
        var positioningStatement = OpportunityCommandSupport.Optional(
            proposal.PositioningStatement, 4000, nameof(proposal.PositioningStatement));
        var proposedSegments = proposal.Audiences.Select(item => new
        {
            Segment = new AudienceSegmentArtifact(
                Guid.NewGuid(), item.Name, item.Description, item.NeedState,
                item.BuyingContext, item.Geographies, item.Language, item.LifeStage,
                item.LsmSem, item.LsmSemTaxonomy, item.LsmSemTaxonomyVersion,
                item.Classification, item.Exclusions, item.EvidenceItemIds,
                item.ReferenceObservationIds, item.Confidence, item.LsmSemMandatory),
            item.IsTarget,
        }).ToArray();
        var targetAudienceIds = proposedSegments
            .Where(item => item.IsTarget)
            .Select(item => item.Segment.Id)
            .ToArray();
        var artifact = new AudienceStrategyArtifact(
            targetAudienceIds, targetingRationale, positioningStatement,
            proposedSegments.Select(item => item.Segment).ToArray());
        var inputHash = PlanningHash.ForBrief(brief);
        var now = timeProvider.GetUtcNow();
        var stored = await IntelligenceArtifactStore.InsertDraftAsync(
            store.DbContext,
            envelope.TenantId,
            envelope.ActorId,
            new IntelligenceArtifactDraft(
                "BriefVersion",
                briefVersionId,
                brief.Version,
                MasterDataCodes.AgentTypes.AudienceIntelligence,
                "audience-strategy.v1",
                Write(artifact),
                proposal.Unknowns,
                [],
                inputHash,
                [proposal.Usage],
                [new IntelligenceArtifactDependencyInput(
                    "BriefVersion", briefVersionId, brief.Version, "commercial_problem")],
                proposedSegments.SelectMany(item =>
                    item.Segment.EvidenceItemIds.Select(evidenceId =>
                        new IntelligenceArtifactEvidenceInput(
                            $"artifact.segments.{item.Segment.Id:N}",
                            item.Segment.Classification,
                            evidenceId,
                            null,
                            "Approved Brief evidence used by Audience Intelligence."))
                    .Concat(item.Segment.ReferenceObservationIds.Select(observationId =>
                        new IntelligenceArtifactEvidenceInput(
                            $"artifact.segments.{item.Segment.Id:N}",
                            item.Segment.Classification,
                            null,
                            observationId,
                            "Governed aggregate reference observation used by Audience Intelligence."))))
                    .ToArray()),
            now,
            cancellationToken);
        var row = await store.FindAudienceAsync(
            envelope.TenantId, stored.Id, cancellationToken)
            ?? throw new InvalidOperationException("The Audience Intelligence artifact was not persisted.");
        var view = PlanningRecordStore.BuildAudienceView(row);
        return OpportunityCommandSupport.Outcome(
            envelope, view, stored.Id, stored.Version,
            MasterDataReferences.CommercialResourceTypes.IntelligenceArtifact,
            MasterDataReferences.CommercialActions.AudienceIntelligenceGenerated,
            MasterDataReferences.CommercialEventTypes.AudienceIntelligenceGenerated, now);
    }

    private async Task<CommandOutcome> GenerateMediaMixOutcomeAsync(
        Guid briefVersionId,
        CommandEnvelope<GenerateMediaMixCommand> envelope,
        CancellationToken cancellationToken)
    {
        var brief = await LoadBudgetReadyBriefAsync(
            briefVersionId, envelope, cancellationToken);
        var (mediaStrategy, audience) = await RequireCurrentApprovedMediaStrategyAsync(
            envelope.TenantId,
            briefVersionId,
            brief.Version,
            cancellationToken);
        var mediaStrategyArtifact = JsonSerializer.Deserialize<MediaStrategyIntelligenceArtifact>(
            mediaStrategy.ArtifactJson, StoredJson)
            ?? throw new InvalidOperationException("The approved Media Strategy Intelligence artifact is invalid.");
        var allocations = BuildWorksheetAllocations(
            mediaStrategyArtifact,
            brief.BudgetMinor!.Value);
        EnsureAllocations(allocations, brief.BudgetMinor.Value);
        var latest = await store.FindLatestMixAsync(
            envelope.TenantId, briefVersionId, cancellationToken);
        var id = Guid.NewGuid();
        var versionNumber = (latest?.VersionNumber ?? 0) + 1;
        var campaignMode = await RequireCampaignModeAsync(
            envelope.TenantId, briefVersionId, cancellationToken);
        campaignModePolicy.EnsureAllocations(campaignMode.Mode, allocations);
        var allocationsJson = Write(allocations);
        var rolesJson = Write(allocations.ToDictionary(item => item.Channel, item => item.Role));
        var assumptionsJson = Write(mediaStrategyArtifact.EvidenceGaps.ToArray());
        var evidenceJson = brief.EvidenceIdsJson;
        var inputHash = IntelligenceInputHash.Combine(
            PlanningHash.ForMix(brief, audience.Id, allocationsJson),
            mediaStrategy.Id.ToString("N"),
            mediaStrategy.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.media_mix_versions (
                id, tenant_id, brief_version_id, audience_artifact_id, media_strategy_artifact_id, version_no,
                total_budget_minor, currency_code, allocations_json, channel_roles_json,
                assumptions_json, evidence_item_ids_json, input_hash,
                status_code, created_by, version, created_at_utc)
            VALUES ({id}, {envelope.TenantId.Value}, {briefVersionId}, {audience.Id}, {mediaStrategy.Id},
                {versionNumber}, {brief.BudgetMinor.Value}, {brief.Currency},
                {allocationsJson}::jsonb, {rolesJson}::jsonb, {assumptionsJson}::jsonb,
                {evidenceJson}::jsonb, {inputHash},
                {MasterDataCodes.LifecycleStatuses.Draft}, {envelope.ActorId.Value}, 1, {now})
            """, cancellationToken);
        var row = await store.FindMixAsync(envelope.TenantId, id, cancellationToken)
            ?? throw new InvalidOperationException("The media mix was not persisted.");
        var view = PlanningRecordStore.BuildMixView(row);
        return OpportunityCommandSupport.Outcome(
            envelope, view, id, row.Version, MasterDataReferences.CommercialResourceTypes.MediaMixVersion,
            MasterDataReferences.CommercialActions.MediaMixGenerated, MasterDataReferences.CommercialEventTypes.MediaMixGenerated, now);
    }

}