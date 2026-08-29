using System.Text.Json;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Brief;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityRunProcessor
{
    private async Task PersistBriefAsync(
        RunExecutionContext context,
        RunStrategyRow strategy,
        AgentStepExecution execution,
        CancellationToken cancellationToken)
    {
        var generated = ParseBrief(context, execution.Output);
        await using var transaction = await runStore.BeginSessionAsync(
            context.ActorId, context.TenantId, cancellationToken);
        var validated = await BriefCommandSupport.ValidateAsync(
            store.DbContext, generated.Command, cancellationToken);
        await PersistSuccessfulStepAsync(context, execution, cancellationToken);
        var briefId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await InsertBriefAggregateAsync(
            context, briefId, now, cancellationToken);
        await InsertBriefSourceAsync(
            context, strategy, briefId, sourceId, now, cancellationToken);
        await InsertGeneratedVersionAsync(
            context, execution, generated.Command, validated,
            briefId, sourceId, versionId, now, cancellationToken);
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.campaign_briefs
            SET current_draft_version_id = {versionId}, version = 2
            WHERE tenant_id = {context.TenantId.Value} AND id = {briefId}
            """, cancellationToken);
        foreach (var evidence in context.Evidence)
        {
            await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.brief_version_evidence_items (
                    tenant_id, brief_version_id, evidence_item_id)
                VALUES ({context.TenantId.Value}, {versionId}, {evidence.Id})
                """, cancellationToken);
        }
        await CompleteRunCoreAsync(context, execution.StepCode, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task<int> InsertBriefAggregateAsync(
        RunExecutionContext context,
        Guid briefId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.campaign_briefs (
                id, tenant_id, client_account_id, opportunity_id, title,
                owner_user_id, status_code, current_draft_version_id,
                version, created_at_utc, updated_at_utc)
            VALUES (
                {briefId}, {context.TenantId.Value}, {context.Opportunity.ClientId},
                {context.Opportunity.Id}, {context.Opportunity.Title + " campaign brief"},
                {context.Opportunity.OwnerUserId}, {MasterDataCodes.LifecycleStatuses.Draft}, NULL,
                1, {now}, {now})
            """, cancellationToken);

    private Task<int> InsertBriefSourceAsync(
        RunExecutionContext context,
        RunStrategyRow strategy,
        Guid briefId,
        Guid sourceId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.brief_sources (
                id, tenant_id, brief_id, source_type_code, locator, title,
                content, content_hash, created_by, created_at_utc)
            VALUES (
                {sourceId}, {context.TenantId.Value}, {briefId},
                {MasterDataCodes.BriefSourceTypes.Opportunity}, {"strategy:" + strategy.Id},
                {"Approved opportunity strategy"}, {strategy.ArtifactJson},
                {OpportunityCommandSupport.Hash(strategy.ArtifactJson)},
                {context.ActorId.Value}, {now})
            """, cancellationToken);

    private Task<int> InsertGeneratedVersionAsync(
        RunExecutionContext context,
        AgentStepExecution execution,
        CreateBriefVersionCommand command,
        ValidatedBriefVersion value,
        Guid briefId,
        Guid sourceId,
        Guid versionId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.brief_versions (
                id, tenant_id, brief_id, source_id, version_no, business_problem,
                objective, audiences_json, geographies_json, timing, budget_minor,
                budget_unknown, currency_code, vat_status_code, fees_minor,
                constraints_json, measurement_json, facts_json, unknowns_json,
                assumptions_json, conflicts_json, evidence_bindings_json, status_code,
                created_by, version, created_at_utc)
            VALUES (
                {versionId}, {context.TenantId.Value}, {briefId}, {sourceId}, 1,
                {value.BusinessProblem}, {value.Objective},
                {BriefCommandSupport.Json(value.Audiences)}::jsonb,
                {BriefCommandSupport.Json(value.Geographies)}::jsonb, {value.Timing},
                {command.BudgetMinor}, {command.BudgetUnknown}, {value.Currency},
                {value.VatStatus}, {command.FeesMinor},
                {BriefCommandSupport.Json(value.Constraints)}::jsonb,
                {BriefCommandSupport.Json(value.Measurement)}::jsonb,
                {BriefCommandSupport.Json(value.Facts)}::jsonb,
                {BriefCommandSupport.Json(value.Unknowns)}::jsonb,
                {BriefCommandSupport.Json(value.Assumptions)}::jsonb,
                {BriefCommandSupport.Json(value.Conflicts)}::jsonb,
                {execution.Output.EvidenceBindings.GetRawText()}::jsonb,
                {MasterDataCodes.LifecycleStatuses.Draft}, {context.ActorId.Value}, 1, {now})
            """, cancellationToken);

    private static GeneratedBriefDraft ParseBrief(
        RunExecutionContext context,
        OpportunityAgentOutput output)
    {
        RequireObjectArtifact(output.Artifact);
        var artifact = output.Artifact;
        var unknowns = JsonSerializer.Deserialize<BriefUnknownInput[]>(
            output.Unknowns.GetRawText(), BriefCommandSupport.StoredJson) ?? [];
        var assumptions = JsonSerializer.Deserialize<BriefAssumptionInput[]>(
            output.Assumptions.GetRawText(), BriefCommandSupport.StoredJson) ?? [];
        var conflicts = JsonSerializer.Deserialize<BriefConflictInput[]>(
            artifact.GetProperty("conflicts").GetRawText(), BriefCommandSupport.StoredJson) ?? [];
        var command = new CreateBriefVersionCommand(
            Guid.Empty, null,
            RequiredText(artifact, "business_problem"),
            RequiredText(artifact, "objective"),
            Strings(artifact, "audiences"),
            Strings(artifact, "geographies"),
            RequiredText(artifact, "timing"),
            OptionalInt64(artifact, "budget_minor"),
            artifact.GetProperty("budget_unknown").GetBoolean(),
            OptionalText(artifact, "currency"),
            OptionalText(artifact, "vat_status"),
            OptionalInt64(artifact, "fees_minor"),
            Strings(artifact, "constraints"),
            Strings(artifact, "measurement"),
            Strings(artifact, "facts"), unknowns, assumptions, conflicts,
            context.Evidence.Select(item => item.Id).ToArray());
        return new GeneratedBriefDraft(command);
    }

    private static string RequiredText(JsonElement parent, string name) =>
        parent.GetProperty(name).GetString()
        ?? throw new InvalidOperationException("A required Brief field was absent.");

    private static string? OptionalText(JsonElement parent, string name) =>
        parent.GetProperty(name).ValueKind == JsonValueKind.Null
            ? null
            : parent.GetProperty(name).GetString();

    private static long? OptionalInt64(JsonElement parent, string name) =>
        parent.GetProperty(name).ValueKind == JsonValueKind.Null
            ? null
            : parent.GetProperty(name).GetInt64();

    private static string[] Strings(JsonElement parent, string name) =>
        parent.GetProperty(name).EnumerateArray()
            .Select(item => item.GetString()
                ?? throw new InvalidOperationException("A Brief list item was invalid."))
            .ToArray();
}

internal sealed record GeneratedBriefDraft(CreateBriefVersionCommand Command);
