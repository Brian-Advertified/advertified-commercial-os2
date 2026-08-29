using System.Text.Json;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityRunProcessor
{
    private async Task PersistInterpretationAsync(
        RunExecutionContext context,
        AgentStepExecution execution,
        CancellationToken cancellationToken)
    {
        RequireObjectArtifact(execution.Output.Artifact);
        await using var transaction = await runStore.BeginSessionAsync(
            context.ActorId, context.TenantId, cancellationToken);
        await PersistSuccessfulStepAsync(context, execution, cancellationToken);
        var version = await NextVersionAsync(
            "business_interpretations", context, cancellationToken);
        var interpretationId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.business_interpretations (
                id, tenant_id, opportunity_id, agent_run_id, evidence_set_id,
                version_no, artifact_json, evidence_bindings_json, unknowns_json,
                assumptions_json, status_code, created_by, version, created_at_utc)
            VALUES (
                {interpretationId}, {context.TenantId.Value}, {context.Run.OpportunityId},
                {context.Run.Id}, {context.Evidence[0].EvidenceSetId}, {version},
                {execution.Output.Artifact.GetRawText()}::jsonb,
                {execution.Output.EvidenceBindings.GetRawText()}::jsonb,
                {execution.Output.Unknowns.GetRawText()}::jsonb,
                {execution.Output.Assumptions.GetRawText()}::jsonb,
                {MasterDataCodes.LifecycleStatuses.Draft}, {context.ActorId.Value}, 1, {now})
            """, cancellationToken);
        await OpportunityCommandSupport.CreateTaskAsync(
            store.DbContext,
            context.TenantId,
            context.Run.OpportunityId,
            MasterDataCodes.HumanTaskTypes.InterpretationConfirmation,
            "Confirm the business interpretation",
            "Human confirmation is required before opportunity angles can be generated.",
            MasterDataReferences.CommercialResourceTypes.BusinessInterpretation,
            interpretationId,
            1,
            context.ActorId.Value,
            now,
            cancellationToken);
        await CompleteRunCoreAsync(context, execution.StepCode, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task PersistAnglesAsync(
        RunExecutionContext context,
        RunInterpretationRow interpretation,
        AgentStepExecution execution,
        CancellationToken cancellationToken)
    {
        var angles = ParseAngles(context, execution.Output.Artifact);
        await using var transaction = await runStore.BeginSessionAsync(
            context.ActorId, context.TenantId, cancellationToken);
        await PersistSuccessfulStepAsync(context, execution, cancellationToken);
        var version = await NextVersionAsync(
            "opportunity_angle_sets", context, cancellationToken);
        var angleSetId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.opportunity_angle_sets (
                id, tenant_id, opportunity_id, agent_run_id, interpretation_id,
                version_no, status_code, created_by, version, created_at_utc)
            VALUES (
                {angleSetId}, {context.TenantId.Value}, {context.Run.OpportunityId},
                {context.Run.Id}, {interpretation.Id}, {version}, {MasterDataCodes.LifecycleStatuses.Draft},
                {context.ActorId.Value}, 1, {now})
            """, cancellationToken);
        foreach (var angle in angles)
        {
            await InsertAngleAsync(context, angleSetId, angle, cancellationToken);
        }
        await OpportunityCommandSupport.CreateTaskAsync(
            store.DbContext,
            context.TenantId,
            context.Run.OpportunityId,
            MasterDataCodes.HumanTaskTypes.AngleSelection,
            "Select an opportunity angle",
            "One evidence-backed angle must be selected before strategy generation.",
            MasterDataReferences.CommercialResourceTypes.OpportunityAngle,
            angleSetId,
            1,
            context.ActorId.Value,
            now,
            cancellationToken);
        await CompleteRunCoreAsync(context, execution.StepCode, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<RunStrategyRow> PersistStrategyAsync(
        RunExecutionContext context,
        RunInterpretationRow interpretation,
        RunAngleRow angle,
        AgentStepExecution execution,
        CancellationToken cancellationToken)
    {
        RequireObjectArtifact(execution.Output.Artifact);
        await using var transaction = await runStore.BeginSessionAsync(
            context.ActorId, context.TenantId, cancellationToken);
        await PersistSuccessfulStepAsync(context, execution, cancellationToken);
        var version = await NextVersionAsync("strategy_versions", context, cancellationToken);
        var strategyId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.strategy_versions (
                id, tenant_id, opportunity_id, agent_run_id, evidence_set_id,
                interpretation_id, selected_angle_id, version_no, artifact_json,
                evidence_bindings_json, unknowns_json, assumptions_json, status_code,
                created_by, version, created_at_utc)
            VALUES (
                {strategyId}, {context.TenantId.Value}, {context.Run.OpportunityId},
                {context.Run.Id}, {context.Evidence[0].EvidenceSetId},
                {interpretation.Id}, {angle.Id}, {version},
                {execution.Output.Artifact.GetRawText()}::jsonb,
                {execution.Output.EvidenceBindings.GetRawText()}::jsonb,
                {execution.Output.Unknowns.GetRawText()}::jsonb,
                {execution.Output.Assumptions.GetRawText()}::jsonb,
                {MasterDataCodes.LifecycleStatuses.Draft}, {context.ActorId.Value}, 1, {now})
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RunStrategyRow
        {
            Id = strategyId,
            VersionNumber = version,
            ArtifactJson = execution.Output.Artifact.GetRawText(),
            Version = 1,
        };
    }

    private async Task PersistCriticAsync(
        RunExecutionContext context,
        RunStrategyRow strategy,
        AgentStepExecution execution,
        CancellationToken cancellationToken)
    {
        RequireObjectArtifact(execution.Output.Artifact);
        await using var transaction = await runStore.BeginSessionAsync(
            context.ActorId, context.TenantId, cancellationToken);
        await PersistSuccessfulStepAsync(context, execution, cancellationToken);
        var reportId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.critic_reports (
                id, tenant_id, agent_run_id, strategy_version_id, artifact_json,
                created_at_utc)
            VALUES (
                {reportId}, {context.TenantId.Value}, {context.Run.Id}, {strategy.Id},
                {execution.Output.Artifact.GetRawText()}::jsonb, {now})
            """, cancellationToken);
        foreach (var objection in execution.Output.Objections)
        {
            await InsertObjectionAsync(
                context, strategy, reportId, objection, now, cancellationToken);
        }
        await CompleteRunCoreAsync(context, execution.StepCode, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task InsertAngleAsync(
        RunExecutionContext context,
        Guid angleSetId,
        GeneratedAngle angle,
        CancellationToken cancellationToken)
    {
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.opportunity_angles (
                id, tenant_id, angle_set_id, rank, title, rationale,
                evidence_item_ids_json, confidence, status_code, version)
            VALUES (
                {Guid.NewGuid()}, {context.TenantId.Value}, {angleSetId}, {angle.Rank},
                {angle.Title}, {angle.Rationale}, {angle.EvidenceIdsJson}::jsonb,
                {angle.Confidence}, {MasterDataCodes.OpportunityAngleStatuses.Proposed}, 1)
            """, cancellationToken);
    }

    private async Task InsertObjectionAsync(
        RunExecutionContext context,
        RunStrategyRow strategy,
        Guid reportId,
        AgentObjectionOutput objection,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await OpportunityCommandSupport.EnsureCodeAsync(
            store.DbContext, MasterDataCodes.CriticSeverities.Collection, objection.Severity, cancellationToken);
        var objectionId = Guid.NewGuid();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.critic_objections (
                id, tenant_id, critic_report_id, severity_code, field_path,
                evidence_gap, recommended_resolution, version)
            VALUES (
                {objectionId}, {context.TenantId.Value}, {reportId}, {objection.Severity},
                {objection.FieldPath}, {objection.EvidenceGap},
                {objection.RecommendedResolution}, 1)
            """, cancellationToken);
        await OpportunityCommandSupport.CreateTaskAsync(
            store.DbContext,
            context.TenantId,
            context.Run.OpportunityId,
            MasterDataCodes.HumanTaskTypes.CriticResolution,
            "Resolve a strategy objection",
            "Every critic objection must be explicitly resolved before submission.",
            MasterDataReferences.CommercialResourceTypes.Strategy,
            objectionId,
            1,
            context.ActorId.Value,
            now,
            cancellationToken);
    }

    private Task<int> NextVersionAsync(
        string table,
        RunExecutionContext context,
        CancellationToken cancellationToken)
    {
        FormattableString query = table switch
        {
            "business_interpretations" => $"""
                SELECT (COALESCE(max(version_no), 0) + 1)::integer AS "Value"
                FROM commercial.business_interpretations
                WHERE tenant_id = {context.TenantId.Value}
                  AND opportunity_id = {context.Run.OpportunityId}
                """,
            "opportunity_angle_sets" => $"""
                SELECT (COALESCE(max(version_no), 0) + 1)::integer AS "Value"
                FROM commercial.opportunity_angle_sets
                WHERE tenant_id = {context.TenantId.Value}
                  AND opportunity_id = {context.Run.OpportunityId}
                """,
            "strategy_versions" => $"""
                SELECT (COALESCE(max(version_no), 0) + 1)::integer AS "Value"
                FROM commercial.strategy_versions
                WHERE tenant_id = {context.TenantId.Value}
                  AND opportunity_id = {context.Run.OpportunityId}
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };
        return store.DbContext.Database.SqlQuery<int>(query).SingleAsync(cancellationToken);
    }

    private static GeneratedAngle[] ParseAngles(
        RunExecutionContext context,
        JsonElement artifact)
    {
        RequireObjectArtifact(artifact);
        var approved = context.Evidence.Select(item => item.Id).ToHashSet();
        var angles = artifact.GetProperty("angles").EnumerateArray()
            .Select(item => new GeneratedAngle(
                item.GetProperty("rank").GetInt32(),
                item.GetProperty("title").GetString()!,
                item.GetProperty("rationale").GetString()!,
                item.GetProperty("evidence_item_ids").GetRawText(),
                item.GetProperty("confidence").GetDecimal(),
                item.GetProperty("evidence_item_ids").EnumerateArray()
                    .Select(id => id.GetGuid()).ToArray()))
            .ToArray();
        if (angles.Length is < 2 or > 3 ||
            angles.Select(item => item.Rank).Distinct().Count() != angles.Length ||
            angles.Any(item => item.EvidenceIds.Count == 0 ||
                item.EvidenceIds.Any(id => !approved.Contains(id))))
        {
            throw new InvalidOperationException("The opportunity angle output is invalid.");
        }
        return angles;
    }

    private static void RequireObjectArtifact(JsonElement artifact)
    {
        if (artifact.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("The agent artifact must be a JSON object.");
        }
    }
}

internal sealed record GeneratedAngle(
    int Rank,
    string Title,
    string Rationale,
    string EvidenceIdsJson,
    decimal Confidence,
    IReadOnlyList<Guid> EvidenceIds);
