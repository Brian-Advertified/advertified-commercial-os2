using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands
{
    private async Task<CommandOutcome> ReviewInterpretationAsync(InventoryCandidateRow row,
        InventoryImportRow source, InventoryAcceptanceArtifact artifact,
        CommandEnvelope<ReviewInventoryCandidateCommand> envelope, CancellationToken cancellationToken)
    {
        if (source.PublishedReleaseId is not null || source.Status != MasterDataCodes.LifecycleStatuses.ReviewRequired)
            throw new InvalidLifecycleTransitionException();
        var codes = await InventoryCodeSets.LoadAsync(store.DbContext, cancellationToken);
        var now = timeProvider.GetUtcNow();
        await CorrectInterpretationAsync(row, source, artifact, envelope, codes, now, cancellationToken);
        var evidence = await store.ListEvidenceAsync(envelope.TenantId, row.Id, cancellationToken);
        return OpportunityCommandSupport.Outcome(envelope,
            (row with { Version = row.Version + 1, UpdatedAtUtc = now }).ToView(evidence), row.Id, row.Version + 1,
            MasterDataReferences.CommercialResourceTypes.InventoryCandidate,
            MasterDataReferences.CommercialActions.InventoryCandidateReviewed,
            MasterDataReferences.CommercialEventTypes.InventoryCandidateReviewed, now);
    }

    private async Task CorrectInterpretationAsync(InventoryCandidateRow row, InventoryImportRow source,
        InventoryAcceptanceArtifact artifact, CommandEnvelope<ReviewInventoryCandidateCommand> envelope,
        InventoryCodeSets codes, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var corrected = ResolveInterpretation(artifact, envelope, now, codes);
        await PersistInterpretationAsync(source, artifact, corrected, envelope.ActorId.Value,
            codes, now, cancellationToken);
        var originalValues = System.Text.Json.JsonSerializer.Deserialize<InventoryCandidateValues>(
            row.ValuesJson, InventoryRowMapper.StoredJson) ?? throw new InventoryExtractionUnavailableException();
        await InsertReviewDecisionAsync(envelope, row, MasterDataCodes.InventoryReviewDecisions.Edit,
            originalValues, now, cancellationToken);
    }

    private static InventoryExtractionResult ResolveInterpretation(InventoryAcceptanceArtifact artifact,
        CommandEnvelope<ReviewInventoryCandidateCommand> envelope, DateTimeOffset now, InventoryCodeSets codes)
    {
        if (envelope.Command.Decision.Equals(MasterDataCodes.InventoryReviewDecisions.Edit, StringComparison.OrdinalIgnoreCase))
            return InventoryInterpretationRevision.Correct(artifact.Extraction(), envelope.Command,
                envelope.ActorId.Value, now, codes);
        if (envelope.Command.CorrectedValues is not null || envelope.Command.CorrectedSchema is not null)
            throw new ArgumentException("Use an interpretation edit to change source mappings.");
        return artifact.Extraction(); // Policy-only reevaluation neither changes mappings nor calls a provider.
    }

    private async Task<IReadOnlyList<CandidateAcceptanceAudit>> VerifyRetainedAcceptanceAsync(InventoryImportRow source,
        IReadOnlyList<InventoryCandidateRow> candidates, InventoryCodeSets codes, CancellationToken cancellationToken)
    {
        var decisions = new List<CandidateAcceptanceAudit>();
        foreach (var group in candidates.Where(item => item.Status == MasterDataCodes.LifecycleStatuses.Approved)
            .GroupBy(item => item.ProjectionId))
        {
            var artifact = await InventoryRetainedAcceptance.LoadAsync(store.DbContext,
                new TenantId(source.TenantId), group.First().Id, cancellationToken);
            if (artifact.Extraction().Document.DiscoveredSchema is null) throw new InventoryPublishBlockedException();
            var evaluated = InventoryRetainedAcceptance.Evaluate(artifact, source, codes, timeProvider.GetUtcNow())
                .ToDictionary(item => item.RowNumber);
            foreach (var row in group)
            {
                InventoryRetainedAcceptance.EnsureMatches(row,
                    evaluated.TryGetValue(row.RowNumber, out var value) ? [value] : []);
                decisions.Add(new(row.Id, InventoryAcceptancePolicy.Read(evaluated[row.RowNumber].Values)
                    ?? throw new InventoryPublishBlockedException()));
            }
        }
        return decisions;
    }

    private sealed record CandidateAcceptanceAudit(Guid CandidateId, InventoryAcceptanceEvaluation Evaluation);

    private Task<int> UpdateInterpretationReviewStepAsync(TenantId tenant, Guid importId,
        bool needsReview, DateTimeOffset now, CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_import_steps (
                id, tenant_id, import_id, step_type_code, status_code,
                outcome_json, started_at_utc, completed_at_utc)
            VALUES (gen_random_uuid(), {tenant.Value}, {importId}, {MasterDataCodes.InventoryImportStepTypes.Review},
                {(needsReview ? MasterDataCodes.LifecycleStatuses.ReviewRequired : MasterDataCodes.LifecycleStatuses.Completed)},
                jsonb_build_object('acceptancePolicyVersion', {InventoryAcceptancePolicy.Version}), {now},
                {(needsReview ? (DateTimeOffset?)null : now)})
            ON CONFLICT (tenant_id, import_id, step_type_code) DO UPDATE
            SET status_code = EXCLUDED.status_code, outcome_json = EXCLUDED.outcome_json,
                completed_at_utc = EXCLUDED.completed_at_utc
            """, cancellationToken);
}
