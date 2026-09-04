using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands
{
    private async Task<CommandOutcome> ReviewOutcomeAsync(
        Guid candidateId,
        CommandEnvelope<ReviewInventoryCandidateCommand> envelope,
        CancellationToken cancellationToken)
    {
        var row = await store.FindCandidateAsync(
            envelope.TenantId, candidateId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory candidate access denied.");
        var source = await store.FindImportAsync(
            envelope.TenantId, row.ImportId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory import access denied.");
        await EnsureAssignedReviewerAsync(
            envelope.TenantId, candidateId, source.CreatedBy,
            envelope.ActorId.Value, cancellationToken);
        if (row.Status != MasterDataCodes.LifecycleStatuses.ReviewRequired)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var decision = NormalizeDecision(envelope.Command.Decision);
        var values = ResolveReviewValues(row, envelope.Command, decision);
        var codes = await InventoryCodeSets.LoadAsync(store.DbContext, cancellationToken);
        var validation = InventoryCandidateValidator.Validate(values, codes);
        ValidateDecision(decision, envelope.Command, validation);
        var now = timeProvider.GetUtcNow();
        var status = decision == MasterDataCodes.InventoryReviewDecisions.Reject
            ? MasterDataCodes.LifecycleStatuses.Rejected : MasterDataCodes.LifecycleStatuses.Approved;
        await ChangeCandidateAsync(
            envelope, row, values, validation, status, now, cancellationToken);
        await InsertReviewDecisionAsync(
            envelope, row, decision, values, now, cancellationToken);
        await CompleteReviewTaskAsync(
            envelope.TenantId, candidateId, envelope.ActorId.Value, now, cancellationToken);
        await CompleteReviewStepWhenReadyAsync(
            envelope.TenantId, row.ImportId, now, cancellationToken);
        var changed = row with
        {
            Status = status,
            ValuesJson = JsonSerializer.Serialize(values, InventoryRowMapper.StoredJson),
            ValidationJson = JsonSerializer.Serialize(validation, InventoryRowMapper.StoredJson),
            ReviewedBy = envelope.ActorId.Value,
            Version = row.Version + 1,
            UpdatedAtUtc = now,
        };
        var evidence = await store.ListEvidenceAsync(
            envelope.TenantId, candidateId, cancellationToken);
        var view = changed.ToView(evidence);
        return OpportunityCommandSupport.Outcome(
            envelope, view, candidateId, changed.Version,
            MasterDataReferences.CommercialResourceTypes.InventoryCandidate,
            MasterDataReferences.CommercialActions.InventoryCandidateReviewed,
            MasterDataReferences.CommercialEventTypes.InventoryCandidateReviewed, now);
    }

    private async Task EnsureAssignedReviewerAsync(
        TenantId tenantId,
        Guid candidateId,
        Guid creatorId,
        Guid reviewerId,
        CancellationToken cancellationToken)
    {
        if (creatorId == reviewerId)
        {
            throw new ApprovalRequiredException();
        }
        var assigned = await store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.human_tasks
                WHERE tenant_id = {tenantId.Value} AND resource_id = {candidateId}
                  AND task_type_code = {MasterDataCodes.HumanTaskTypes.InventoryCandidateReview}
                  AND assignee_user_id = {reviewerId}
                  AND status_code = {MasterDataCodes.LifecycleStatuses.Pending}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!assigned)
        {
            throw new ApprovalRequiredException();
        }
    }

    private static InventoryCandidateValues ResolveReviewValues(
        InventoryCandidateRow row,
        ReviewInventoryCandidateCommand command,
        string decision)
    {
        if (decision == MasterDataCodes.InventoryReviewDecisions.Edit)
        {
            return InventoryReviewSupport.NormalizeCorrection(command.CorrectedValues
                ?? throw new ArgumentException("Corrected values are required for an edit."));
        }
        if (command.CorrectedValues is not null)
        {
            throw new ArgumentException("Use the edit decision when correcting fields.");
        }
        return JsonSerializer.Deserialize<InventoryCandidateValues>(
            row.ValuesJson, InventoryRowMapper.StoredJson)
            ?? throw new InvalidOperationException("Stored inventory values are invalid.");
    }

    private static string NormalizeDecision(string value)
    {
        var decision = value?.Trim().ToUpperInvariant();
        return decision is MasterDataCodes.InventoryReviewDecisions.Approve or MasterDataCodes.InventoryReviewDecisions.Reject
            or MasterDataCodes.InventoryReviewDecisions.Edit
            ? decision
            : throw new ArgumentException("Select a supported review decision.");
    }

    private static void ValidateDecision(
        string decision,
        ReviewInventoryCandidateCommand command,
        IReadOnlyList<InventoryValidationIssueView> validation)
    {
        if (decision == MasterDataCodes.InventoryReviewDecisions.Reject)
        {
            if (string.IsNullOrWhiteSpace(command.RejectionReason))
            {
                throw new ArgumentException("A rejection reason is required.");
            }
            return;
        }
        if (validation.Any(issue => issue.IsBlocking))
        {
            throw new InventoryPublishBlockedException();
        }
    }

    private async Task ChangeCandidateAsync(
        CommandEnvelope<ReviewInventoryCandidateCommand> envelope,
        InventoryCandidateRow row,
        InventoryCandidateValues values,
        IReadOnlyList<InventoryValidationIssueView> validation,
        string status,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var valuesJson = JsonSerializer.Serialize(values, InventoryRowMapper.StoredJson);
        var validationJson = JsonSerializer.Serialize(validation, InventoryRowMapper.StoredJson);
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_candidates
            SET status_code = {status}, canonical_values_json = {valuesJson}::jsonb,
                validation_json = {validationJson}::jsonb,
                reviewed_by = {envelope.ActorId.Value}, version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {row.Id}
              AND status_code = {MasterDataCodes.LifecycleStatuses.ReviewRequired}
              AND superseded_at_utc IS NULL
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
    }

    private async Task InsertReviewDecisionAsync(
        CommandEnvelope<ReviewInventoryCandidateCommand> envelope,
        InventoryCandidateRow row,
        string decision,
        InventoryCandidateValues values,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var reason = envelope.Command.RejectionReason?.Trim().ToUpperInvariant();
        if (reason is not null)
        {
            await OpportunityCommandSupport.EnsureCodeAsync(
                store.DbContext, MasterDataCodes.RejectionReasons.Collection, reason, cancellationToken);
        }
        var correction = decision == MasterDataCodes.InventoryReviewDecisions.Edit
            ? JsonSerializer.Serialize(values, InventoryRowMapper.StoredJson) : null;
        var notes = OpportunityCommandSupport.Optional(
            envelope.Command.Notes, 2000, nameof(envelope.Command.Notes));
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_review_decisions (
                id, tenant_id, candidate_id, candidate_version, decision_code,
                rejection_reason_collection_code, rejection_reason_code, correction_json,
                notes, decided_by, decided_at_utc)
            VALUES ({Guid.NewGuid()}, {envelope.TenantId.Value}, {row.Id}, {row.Version},
                {decision}, {(reason is null ? null : MasterDataCodes.RejectionReasons.Collection)}, {reason},
                {correction}::jsonb, {notes}, {envelope.ActorId.Value}, {now})
            """, cancellationToken);
    }

    private Task<int> CompleteReviewTaskAsync(
        TenantId tenantId, Guid candidateId, Guid actorId, DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.human_tasks
            SET status_code = {MasterDataCodes.LifecycleStatuses.Completed}, completed_by = {actorId},
                completed_at_utc = {now}, completion_json = {"{\"completed\":true}"}::jsonb,
                version = version + 1
            WHERE tenant_id = {tenantId.Value} AND resource_id = {candidateId}
              AND task_type_code = {MasterDataCodes.HumanTaskTypes.InventoryCandidateReview}
              AND assignee_user_id = {actorId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Pending}
            """, cancellationToken);

    private async Task CompleteReviewStepWhenReadyAsync(
        TenantId tenantId, Guid importId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pending = await store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM commercial.inventory_candidates
                WHERE tenant_id = {tenantId.Value} AND import_id = {importId}
                  AND superseded_at_utc IS NULL
                  AND status_code = {MasterDataCodes.LifecycleStatuses.ReviewRequired}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!pending)
        {
            await RecordStepAsync(tenantId, importId, MasterDataCodes.InventoryImportStepTypes.Review,
                MasterDataCodes.LifecycleStatuses.Completed, now, cancellationToken);
        }
    }
}

internal static class InventoryReviewSupport
{
    internal static InventoryCandidateValues NormalizeCorrection(InventoryCandidateValues values)
    {
        var normalized = InventoryCandidateValueNormalization.Normalize(values);
        InventoryCandidateValueNormalization.EnsureCorrectionLimits(normalized);
        return normalized;
    }
}
