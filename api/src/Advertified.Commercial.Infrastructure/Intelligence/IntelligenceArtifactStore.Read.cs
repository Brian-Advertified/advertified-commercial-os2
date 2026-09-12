using System.Text.Json;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Intelligence;

internal static partial class IntelligenceArtifactStore
{
    internal static async Task<IntelligenceArtifactView?> FindAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        EnsureTransaction(dbContext);
        var row = await dbContext.Database.SqlQuery<IntelligenceArtifactRow>($"""
            SELECT id AS "Id", subject_type AS "SubjectType", subject_id AS "SubjectId",
                subject_version AS "SubjectVersion", service_code AS "ServiceCode",
                artifact_schema_version AS "ArtifactSchemaVersion", version_no AS "VersionNumber",
                artifact_json::text AS "ArtifactJson", unknowns_json::text AS "UnknownsJson",
                assumptions_json::text AS "AssumptionsJson", input_hash AS "InputHash",
                status_code AS "Status", supersedes_artifact_id AS "SupersedesArtifactId",
                created_by AS "CreatedBy", approved_by AS "ApprovedBy",
                created_at_utc AS "CreatedAtUtc", approved_at_utc AS "ApprovedAtUtc",
                version AS "Version"
            FROM commercial.intelligence_artifacts
            WHERE tenant_id = {tenantId.Value} AND id = {artifactId}
            LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : await ToViewAsync(dbContext, tenantId, row, cancellationToken);
    }

    internal static async Task<IntelligenceArtifactView?> FindLatestAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        string subjectType,
        Guid subjectId,
        string serviceCode,
        CancellationToken cancellationToken)
    {
        EnsureTransaction(dbContext);
        if (string.IsNullOrWhiteSpace(subjectType) || string.IsNullOrWhiteSpace(serviceCode))
            throw new ArgumentException("Intelligence artifact lookup scope is invalid.");
        var row = await dbContext.Database.SqlQuery<IntelligenceArtifactRow>($"""
            SELECT id AS "Id", subject_type AS "SubjectType", subject_id AS "SubjectId",
                subject_version AS "SubjectVersion", service_code AS "ServiceCode",
                artifact_schema_version AS "ArtifactSchemaVersion", version_no AS "VersionNumber",
                artifact_json::text AS "ArtifactJson", unknowns_json::text AS "UnknownsJson",
                assumptions_json::text AS "AssumptionsJson", input_hash AS "InputHash",
                status_code AS "Status", supersedes_artifact_id AS "SupersedesArtifactId",
                created_by AS "CreatedBy", approved_by AS "ApprovedBy",
                created_at_utc AS "CreatedAtUtc", approved_at_utc AS "ApprovedAtUtc",
                version AS "Version"
            FROM commercial.intelligence_artifacts
            WHERE tenant_id = {tenantId.Value} AND subject_type = {subjectType}
              AND subject_id = {subjectId} AND service_code = {serviceCode}
            ORDER BY version_no DESC
            LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : await ToViewAsync(dbContext, tenantId, row, cancellationToken);
    }

    internal static Task<IntelligenceArtifactView?> FindLatestApprovedAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        string subjectType,
        Guid subjectId,
        string serviceCode,
        CancellationToken cancellationToken) =>
        FindLatestByStatusAsync(
            dbContext, tenantId, subjectType, subjectId, serviceCode,
            MasterDataCodes.LifecycleStatuses.Approved, cancellationToken);

    private static async Task<IntelligenceArtifactView?> FindLatestByStatusAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        string subjectType,
        Guid subjectId,
        string serviceCode,
        string status,
        CancellationToken cancellationToken)
    {
        EnsureTransaction(dbContext);
        if (string.IsNullOrWhiteSpace(subjectType) || string.IsNullOrWhiteSpace(serviceCode))
            throw new ArgumentException("Intelligence artifact lookup scope is invalid.");
        var row = await dbContext.Database.SqlQuery<IntelligenceArtifactRow>($"""
            SELECT id AS "Id", subject_type AS "SubjectType", subject_id AS "SubjectId",
                subject_version AS "SubjectVersion", service_code AS "ServiceCode",
                artifact_schema_version AS "ArtifactSchemaVersion", version_no AS "VersionNumber",
                artifact_json::text AS "ArtifactJson", unknowns_json::text AS "UnknownsJson",
                assumptions_json::text AS "AssumptionsJson", input_hash AS "InputHash",
                status_code AS "Status", supersedes_artifact_id AS "SupersedesArtifactId",
                created_by AS "CreatedBy", approved_by AS "ApprovedBy",
                created_at_utc AS "CreatedAtUtc", approved_at_utc AS "ApprovedAtUtc",
                version AS "Version"
            FROM commercial.intelligence_artifacts
            WHERE tenant_id = {tenantId.Value} AND subject_type = {subjectType}
              AND subject_id = {subjectId} AND service_code = {serviceCode}
              AND status_code = {status}
            ORDER BY version_no DESC
            LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : await ToViewAsync(dbContext, tenantId, row, cancellationToken);
    }

    internal static async Task<IReadOnlyList<IntelligenceArtifactDependencyInput>> ReadDependenciesAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        EnsureTransaction(dbContext);
        return await dbContext.Database.SqlQuery<IntelligenceArtifactDependencyInput>($"""
            SELECT resource_type AS "ResourceType", resource_id AS "ResourceId",
                resource_version AS "ResourceVersion", purpose_code AS "PurposeCode"
            FROM commercial.intelligence_artifact_dependencies
            WHERE tenant_id = {tenantId.Value} AND artifact_id = {artifactId}
            ORDER BY purpose_code, resource_type, resource_id
            """).ToArrayAsync(cancellationToken);
    }

    private static async Task<IntelligenceArtifactView> ToViewAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        IntelligenceArtifactRow row,
        CancellationToken cancellationToken)
    {
        var invocations = await ReadInvocationsAsync(dbContext, tenantId, row.Id, cancellationToken);
        return new IntelligenceArtifactView(
            row.Id,
            row.SubjectType,
            row.SubjectId,
            row.SubjectVersion,
            row.ServiceCode,
            row.ArtifactSchemaVersion,
            row.VersionNumber,
            row.ArtifactJson,
            JsonSerializer.Deserialize<string[]>(row.UnknownsJson, StoredJson) ?? [],
            JsonSerializer.Deserialize<string[]>(row.AssumptionsJson, StoredJson) ?? [],
            row.InputHash,
            invocations,
            invocations.Sum(item => item.IncrementalCostMinor),
            row.Status,
            row.SupersedesArtifactId,
            row.CreatedBy,
            row.ApprovedBy,
            row.CreatedAtUtc,
            row.ApprovedAtUtc,
            row.Version);
    }

    private static async Task<IReadOnlyList<IntelligenceInvocationUsage>> ReadInvocationsAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<IntelligenceInvocationRow>($"""
            SELECT sequence_no AS "SequenceNo", operation_code AS "OperationCode",
                provider_code AS "Provider", model_code AS "Model",
                incremental_cost_minor AS "IncrementalCostMinor", cache_status AS "CacheStatus",
                provider_request_id AS "ProviderRequestId", input_tokens AS "InputTokens",
                output_tokens AS "OutputTokens",
                incremental_cost_usd_micros AS "IncrementalCostUsdMicros"
            FROM commercial.intelligence_artifact_invocations
            WHERE tenant_id = {tenantId.Value} AND artifact_id = {artifactId}
            ORDER BY sequence_no
            """).ToArrayAsync(cancellationToken);
        return rows.Select(row => new IntelligenceInvocationUsage(
            row.OperationCode,
            row.Provider,
            row.Model,
            row.IncrementalCostMinor,
            row.CacheStatus,
            row.ProviderRequestId,
            row.InputTokens,
            row.OutputTokens,
            row.IncrementalCostUsdMicros)).ToArray();
    }
}
