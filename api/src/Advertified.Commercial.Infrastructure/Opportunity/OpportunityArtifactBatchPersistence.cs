using System.Text.Json;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

internal static class OpportunityArtifactBatchPersistence
{
    private const int MaximumObjections = 100;
    private static readonly JsonSerializerOptions StoredJson =
        new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> CriticSeverities =
        MasterDataRegistryReader.Read().Collections
            .Single(item => item.Code == MasterDataCodes.CriticSeverities.Collection).Items
            .Where(item => item.IsActive).Select(item => item.Code)
            .ToHashSet(StringComparer.Ordinal);

    internal static Task<int> InsertAnglesAsync(
        GovernanceDbContext dbContext,
        RunExecutionContext context,
        Guid angleSetId,
        IReadOnlyList<GeneratedAngle> angles,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            angles.Select(angle => new PreparedAngle(
                Guid.NewGuid(), angle.Rank, angle.Title, angle.Rationale,
                angle.EvidenceIdsJson, angle.Confidence)),
            StoredJson);
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.opportunity_angles (
                id, tenant_id, angle_set_id, rank, title, rationale,
                evidence_item_ids_json, confidence, status_code, version)
            SELECT value."id", {context.TenantId.Value}, {angleSetId}, value."rank",
                value."title", value."rationale", value."evidenceIdsJson"::jsonb,
                value."confidence", {MasterDataCodes.OpportunityAngleStatuses.Proposed}, 1
            FROM jsonb_to_recordset({payload}::jsonb) AS value(
                "id" uuid, "rank" integer, "title" text, "rationale" text,
                "evidenceIdsJson" text, "confidence" numeric)
            """, cancellationToken);
    }

    internal static Task<int> InsertObjectionsAsync(
        GovernanceDbContext dbContext,
        RunExecutionContext context,
        Guid reportId,
        IReadOnlyList<AgentObjectionOutput> objections,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (objections.Count > MaximumObjections)
        {
            throw new InvalidOperationException("The critic returned too many objections.");
        }
        if (objections.Count == 0) return Task.FromResult(0);
        var prepared = objections.Select(PrepareObjection).ToArray();
        var payload = JsonSerializer.Serialize(prepared, StoredJson);
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.critic_objections (
                id, tenant_id, critic_report_id, severity_code, field_path,
                evidence_gap, recommended_resolution, version)
            SELECT value."id", {context.TenantId.Value}, {reportId}, value."severity",
                value."fieldPath", value."evidenceGap", value."recommendedResolution", 1
            FROM jsonb_to_recordset({payload}::jsonb) AS value(
                "id" uuid, "taskId" uuid, "severity" text, "fieldPath" text,
                "evidenceGap" text, "recommendedResolution" text);

            INSERT INTO commercial.human_tasks (
                id, tenant_id, opportunity_id, task_type_code, status_code, title,
                why_it_matters, resource_type_code, resource_id, resource_version,
                assignee_user_id, action_schema_json, version, created_at_utc)
            SELECT value."taskId", {context.TenantId.Value}, {context.Run.OpportunityId},
                {MasterDataCodes.HumanTaskTypes.CriticResolution},
                {MasterDataCodes.LifecycleStatuses.Pending},
                {"Resolve a strategy objection"},
                {"Every critic objection must be explicitly resolved before submission."},
                {MasterDataReferences.CommercialResourceTypes.Strategy.Value},
                value."id", 1, {context.ActorId.Value}, {"{}"}::jsonb, 1, {now}
            FROM jsonb_to_recordset({payload}::jsonb) AS value(
                "id" uuid, "taskId" uuid, "severity" text, "fieldPath" text,
                "evidenceGap" text, "recommendedResolution" text);
            """, cancellationToken);
    }

    private static PreparedObjection PrepareObjection(AgentObjectionOutput objection)
    {
        var severity = Required(objection.Severity, 100).ToUpperInvariant();
        if (!CriticSeverities.Contains(severity))
        {
            throw new InvalidOperationException("The critic returned an invalid severity.");
        }
        return new PreparedObjection(
            Guid.NewGuid(), Guid.NewGuid(), severity,
            Required(objection.FieldPath, 200),
            Required(objection.EvidenceGap, 4_000),
            Required(objection.RecommendedResolution, 4_000));
    }

    private static string Required(string value, int maximum)
    {
        var normalized = value.Trim();
        return normalized.Length is > 0 && normalized.Length <= maximum
            ? normalized
            : throw new InvalidOperationException("The critic returned invalid text.");
    }

    private sealed record PreparedAngle(
        Guid Id,
        int Rank,
        string Title,
        string Rationale,
        string EvidenceIdsJson,
        decimal Confidence);

    private sealed record PreparedObjection(
        Guid Id,
        Guid TaskId,
        string Severity,
        string FieldPath,
        string EvidenceGap,
        string RecommendedResolution);
}
