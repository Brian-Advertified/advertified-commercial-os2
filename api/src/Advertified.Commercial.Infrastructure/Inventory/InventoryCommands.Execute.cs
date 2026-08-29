using System.Security.Cryptography;
using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands
{
    private async Task<CommandOutcome> ExecuteOutcomeAsync(
        Guid importId,
        CommandEnvelope<ExecuteInventoryImportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var source = await store.FindImportAsync(
            envelope.TenantId, importId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory import access denied.");
        if (source.Status != Gate6InventoryStatuses.Uploaded ||
            source.ScanStatus != Gate6ScanStatuses.Clean ||
            source.ProtectedObjectKey is null || source.DocumentClass is null)
        {
            throw new InvalidLifecycleTransitionException();
        }
        if (source.Version != envelope.ExpectedVersion)
        {
            throw new VersionConflictException();
        }
        var reviewer = await FindReviewerAsync(
            envelope.TenantId, source.CreatedBy, cancellationToken);
        var content = await store.ObjectStore.ReadAsync(
            source.ProtectedObjectKey, cancellationToken);
        VerifyHash(content, source.SourceHash);
        var rows = InventorySourceExtractor.Extract(source.DocumentClass, content);
        var codes = await InventoryCodeSets.LoadAsync(store.DbContext, cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var row in rows)
        {
            var extracted = InventoryCandidateNormalizer.Normalize(row, source.SourceHash);
            await InsertCandidateAsync(
                envelope.TenantId, source, extracted, codes, reviewer, now, cancellationToken);
        }
        await CompleteExecutionAsync(
            envelope.TenantId, importId, source.Version, rows.Count, now, cancellationToken);
        var updated = await store.FindImportAsync(
            envelope.TenantId, importId, false, cancellationToken)
            ?? throw new InvalidOperationException("The inventory import was not persisted.");
        var view = await store.BuildImportViewAsync(updated, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, importId, updated.Version,
            CommercialResourceTypes.InventoryImport,
            CommercialActions.InventoryImportExecuted,
            CommercialEventTypes.InventoryImportExecuted, now);
    }

    private async Task<Guid> FindReviewerAsync(
        TenantId tenantId,
        Guid creatorId,
        CancellationToken cancellationToken)
    {
        var reviewers = await store.DbContext.Database.SqlQuery<Guid>($"""
            SELECT membership.user_id AS "Value"
            FROM commercial.memberships membership
            WHERE membership.tenant_id = {tenantId.Value}
              AND membership.user_id <> {creatorId}
              AND membership.status_code = {Gate6InventoryStatuses.Active}
              AND membership.role_code = ANY({Gate6ReviewerRoles.Inventory})
            ORDER BY membership.role_code, membership.user_id
            LIMIT 1
            """).ToListAsync(cancellationToken);
        return reviewers.Count == 1
            ? reviewers[0] : throw new ApprovalRequiredException();
    }

    private static void VerifyHash(byte[] content, string expected)
    {
        var actual = Convert.ToHexStringLower(SHA256.HashData(content));
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actual), Convert.FromHexString(expected)))
        {
            throw new InventoryProtectionUnavailableException();
        }
    }

    private async Task InsertCandidateAsync(
        TenantId tenantId,
        InventoryImportRow source,
        ExtractedInventoryCandidate extracted,
        InventoryCodeSets codes,
        Guid reviewer,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var valuesJson = JsonSerializer.Serialize(
            extracted.Values, InventoryRowMapper.StoredJson);
        var validation = InventoryCandidateValidator.Validate(extracted.Values, codes);
        var validationJson = JsonSerializer.Serialize(validation, InventoryRowMapper.StoredJson);
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_candidates (
                id, tenant_id, import_id, row_number, status_code,
                proposed_values_json, canonical_values_json, validation_json,
                source_locator, version, created_at_utc, updated_at_utc)
            VALUES ({id}, {tenantId.Value}, {source.Id}, {extracted.RowNumber},
                {Gate6InventoryStatuses.ReviewRequired}, {valuesJson}::jsonb,
                {valuesJson}::jsonb, {validationJson}::jsonb, {extracted.Locator},
                1, {now}, {now})
            """, cancellationToken);
        await InsertEvidenceAsync(tenantId, id, extracted.Evidence, cancellationToken);
        await CreateReviewTaskAsync(tenantId, id, reviewer, now, cancellationToken);
    }

    private async Task InsertEvidenceAsync(
        TenantId tenantId,
        Guid candidateId,
        IReadOnlyList<InventoryFieldEvidenceView> evidence,
        CancellationToken cancellationToken)
    {
        foreach (var field in evidence)
        {
            await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.inventory_candidate_fields (
                    id, tenant_id, candidate_id, field_name, raw_value, normalized_value,
                    transformation_code, source_locator, source_hash)
                VALUES ({Guid.NewGuid()}, {tenantId.Value}, {candidateId}, {field.FieldName},
                    {field.RawValue}, {field.NormalizedValue}, {field.Transformation},
                    {field.SourceLocator}, {field.SourceHash})
                """, cancellationToken);
        }
    }

    private Task<int> CreateReviewTaskAsync(
        TenantId tenantId,
        Guid candidateId,
        Guid reviewer,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.human_tasks (
                id, tenant_id, opportunity_id, task_type_code, status_code, title,
                why_it_matters, resource_type_code, resource_id, resource_version,
                assignee_user_id, action_schema_json, version, created_at_utc)
            VALUES ({Guid.NewGuid()}, {tenantId.Value}, NULL,
                {Gate6TaskTypes.CandidateReview}, {"PENDING"}, {"Review inventory candidate"},
                {"Verify source-linked fields before inventory publication."},
                {CommercialResourceTypes.InventoryCandidate.Value}, {candidateId}, 1,
                {reviewer}, {"{}"}::jsonb, 1, {now})
            """, cancellationToken);

    private async Task CompleteExecutionAsync(
        TenantId tenantId,
        Guid importId,
        long expectedVersion,
        int candidateCount,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_imports
            SET status_code = {Gate6InventoryStatuses.ReviewRequired}, version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {tenantId.Value} AND id = {importId}
              AND status_code = {Gate6InventoryStatuses.Uploaded}
              AND version = {expectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        await RecordStepAsync(tenantId, importId, Gate6InventorySteps.Extraction,
            Gate6InventoryStatuses.Completed, now, cancellationToken);
        await RecordStepAsync(tenantId, importId, Gate6InventorySteps.Normalization,
            Gate6InventoryStatuses.Completed, now, cancellationToken);
        await RecordStepAsync(tenantId, importId, Gate6InventorySteps.Validation,
            Gate6InventoryStatuses.Completed, now, cancellationToken);
    }
}
