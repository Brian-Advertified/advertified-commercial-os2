using System.Text.Json;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Intelligence;

internal static partial class IntelligenceArtifactStore
{
    internal static async Task<IntelligenceArtifactView> InsertDraftAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        ActorId actorId,
        IntelligenceArtifactDraft draft,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ValidateDraft(draft);
        EnsureTransaction(dbContext);
        var latest = await FindLatestAsync(
            dbContext, tenantId, draft.SubjectType, draft.SubjectId, draft.ServiceCode,
            cancellationToken);
        var id = Guid.NewGuid();
        var versionNumber = (latest?.VersionNumber ?? 0) + 1;
        var unknownsJson = JsonSerializer.Serialize(draft.Unknowns, StoredJson);
        var assumptionsJson = JsonSerializer.Serialize(draft.Assumptions, StoredJson);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.intelligence_artifacts (
                id, tenant_id, subject_type, subject_id, subject_version,
                service_code, artifact_schema_version, version_no, artifact_json,
                unknowns_json, assumptions_json, input_hash, status_code,
                supersedes_artifact_id, created_by, created_at_utc, version)
            VALUES (
                {id}, {tenantId.Value}, {draft.SubjectType}, {draft.SubjectId}, {draft.SubjectVersion},
                {draft.ServiceCode}, {draft.ArtifactSchemaVersion}, {versionNumber}, {draft.ArtifactJson}::jsonb,
                {unknownsJson}::jsonb, {assumptionsJson}::jsonb, {draft.InputHash},
                {MasterDataCodes.LifecycleStatuses.Draft}, {latest?.Id}, {actorId.Value}, {now}, 1)
            """, cancellationToken);

        await InsertInvocationsAsync(dbContext, tenantId, id, draft.Invocations, cancellationToken);
        await InsertDependenciesAsync(dbContext, tenantId, id, draft.Dependencies, cancellationToken);
        await InsertEvidenceAsync(dbContext, tenantId, id, draft.Evidence, cancellationToken);

        return await FindAsync(dbContext, tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException("The intelligence artifact draft was not persisted.");
    }

    internal static async Task<IntelligenceArtifactView> ApproveRevisionAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        ActorId actorId,
        Guid draftArtifactId,
        long expectedVersion,
        string approvedArtifactJson,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        EnsureTransaction(dbContext);
        ValidateArtifactJson(approvedArtifactJson);
        var draft = await FindAsync(dbContext, tenantId, draftArtifactId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Intelligence artifact access denied.");
        if (draft.Status != MasterDataCodes.LifecycleStatuses.Draft || draft.Version != expectedVersion)
            throw new InvalidOperationException("Only the current draft intelligence artifact can be approved.");
        var latest = await FindLatestAsync(
            dbContext, tenantId, draft.SubjectType, draft.SubjectId, draft.ServiceCode,
            cancellationToken);
        if (latest?.Id != draft.Id)
            throw new InvalidOperationException("A newer intelligence artifact already exists.");

        var approvedId = Guid.NewGuid();
        var versionNumber = draft.VersionNumber + 1;
        var unknownsJson = JsonSerializer.Serialize(draft.Unknowns, StoredJson);
        var assumptionsJson = JsonSerializer.Serialize(draft.Assumptions, StoredJson);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.intelligence_artifacts (
                id, tenant_id, subject_type, subject_id, subject_version,
                service_code, artifact_schema_version, version_no, artifact_json,
                unknowns_json, assumptions_json, input_hash, status_code,
                supersedes_artifact_id, created_by, approved_by,
                created_at_utc, approved_at_utc, version)
            VALUES (
                {approvedId}, {tenantId.Value}, {draft.SubjectType}, {draft.SubjectId}, {draft.SubjectVersion},
                {draft.ServiceCode}, {draft.ArtifactSchemaVersion}, {versionNumber}, {approvedArtifactJson}::jsonb,
                {unknownsJson}::jsonb, {assumptionsJson}::jsonb, {draft.InputHash},
                {MasterDataCodes.LifecycleStatuses.Approved}, {draft.Id}, {draft.CreatedBy},
                {actorId.Value}, {now}, {now}, {expectedVersion + 1})
            """, cancellationToken);

        // Approval is a human state transition, not another model invocation. Do not clone AI usage.
        await CloneDependenciesAsync(dbContext, tenantId, draft.Id, approvedId, cancellationToken);
        await CloneEvidenceAsync(dbContext, tenantId, draft.Id, approvedId, cancellationToken);

        return await FindAsync(dbContext, tenantId, approvedId, cancellationToken)
            ?? throw new InvalidOperationException("The approved intelligence artifact was not persisted.");
    }

    private static async Task InsertInvocationsAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid artifactId,
        IReadOnlyList<IntelligenceInvocationUsage> invocations,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < invocations.Count; index++)
        {
            var item = invocations[index];
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.intelligence_artifact_invocations (
                    tenant_id, artifact_id, sequence_no, operation_code, provider_code,
                    model_code, incremental_cost_minor, cache_status, provider_request_id,
                    input_tokens, output_tokens, incremental_cost_usd_micros)
                VALUES ({tenantId.Value}, {artifactId}, {index + 1}, {item.OperationCode},
                    {item.Provider}, {item.Model}, {item.IncrementalCostMinor}, {item.CacheStatus},
                    {item.ProviderRequestId}, {item.InputTokens}, {item.OutputTokens},
                    {item.IncrementalCostUsdMicros})
                """, cancellationToken);
        }
    }

    private static async Task InsertDependenciesAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid artifactId,
        IReadOnlyList<IntelligenceArtifactDependencyInput> dependencies,
        CancellationToken cancellationToken)
    {
        foreach (var item in dependencies.Distinct())
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.intelligence_artifact_dependencies (
                    tenant_id, artifact_id, resource_type, resource_id, resource_version, purpose_code)
                VALUES ({tenantId.Value}, {artifactId}, {item.ResourceType}, {item.ResourceId},
                    {item.ResourceVersion}, {item.PurposeCode})
                """, cancellationToken);
        }
    }

    private static async Task InsertEvidenceAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid artifactId,
        IReadOnlyList<IntelligenceArtifactEvidenceInput> evidence,
        CancellationToken cancellationToken)
    {
        foreach (var item in evidence.Distinct())
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.intelligence_artifact_evidence (
                    id, tenant_id, artifact_id, field_path, classification_code,
                    evidence_item_id, reference_observation_id, rationale)
                VALUES ({Guid.NewGuid()}, {tenantId.Value}, {artifactId}, {item.FieldPath},
                    {item.ClassificationCode}, {item.EvidenceItemId}, {item.ReferenceObservationId},
                    {item.Rationale})
                """, cancellationToken);
        }
    }

    private static Task<int> CloneDependenciesAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid sourceArtifactId,
        Guid targetArtifactId,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.intelligence_artifact_dependencies (
                tenant_id, artifact_id, resource_type, resource_id, resource_version, purpose_code)
            SELECT tenant_id, {targetArtifactId}, resource_type, resource_id, resource_version, purpose_code
            FROM commercial.intelligence_artifact_dependencies
            WHERE tenant_id = {tenantId.Value} AND artifact_id = {sourceArtifactId}
            """, cancellationToken);

    private static async Task CloneEvidenceAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid sourceArtifactId,
        Guid targetArtifactId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<IntelligenceEvidenceRow>($"""
            SELECT field_path AS "FieldPath", classification_code AS "ClassificationCode",
                evidence_item_id AS "EvidenceItemId", reference_observation_id AS "ReferenceObservationId",
                rationale AS "Rationale"
            FROM commercial.intelligence_artifact_evidence
            WHERE tenant_id = {tenantId.Value} AND artifact_id = {sourceArtifactId}
            """).ToArrayAsync(cancellationToken);
        await InsertEvidenceAsync(
            dbContext,
            tenantId,
            targetArtifactId,
            rows.Select(item => new IntelligenceArtifactEvidenceInput(
                item.FieldPath,
                item.ClassificationCode,
                item.EvidenceItemId,
                item.ReferenceObservationId,
                item.Rationale)).ToArray(),
            cancellationToken);
    }
}
