using System.Text.Json;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityRunProcessor
{
    private async Task<RunExecutionContext> LoadContextAsync(
        RunClaim claim,
        CancellationToken cancellationToken)
    {
        var tenantId = new TenantId(claim.TenantId);
        await using var transaction = await runStore.BeginSessionAsync(
            new ActorId(claim.RequestedBy), tenantId, cancellationToken);
        var run = await runStore.FindWorkAsync(tenantId, claim.RunId, cancellationToken)
            ?? throw new InvalidOperationException("The claimed run no longer exists.");
        var opportunity = await store.FindOpportunityAsync(
            tenantId, run.OpportunityId, cancellationToken)
            ?? throw new InvalidOperationException("The run opportunity no longer exists.");
        var evidence = await runStore.ListApprovedEvidenceAsync(
            tenantId, run.OpportunityId, cancellationToken);
        if (evidence.Count == 0)
        {
            throw new EvidenceRequiredException();
        }

        var interpretation = await LoadInterpretationAsync(
            tenantId, run.OpportunityId, cancellationToken);
        var angle = await LoadSelectedAngleAsync(
            tenantId, run.OpportunityId, cancellationToken);
        var strategy = await LoadRunStrategyAsync(tenantId, run.Id, cancellationToken);
        var criticExists = strategy is not null && await CriticExistsAsync(
            tenantId, strategy.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RunExecutionContext(
            tenantId, new ActorId(claim.RequestedBy), run, opportunity, evidence,
            interpretation, angle, strategy, criticExists);
    }

    private Task<RunInterpretationRow?> LoadInterpretationAsync(
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<RunInterpretationRow>($"""
            SELECT id AS "Id", version_no AS "VersionNumber",
                artifact_json::text AS "ArtifactJson", version AS "Version"
            FROM commercial.business_interpretations
            WHERE tenant_id = {tenantId.Value} AND opportunity_id = {opportunityId}
              AND status_code = {Gate4Statuses.Approved}
            ORDER BY version_no DESC
            LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);

    private Task<RunAngleRow?> LoadSelectedAngleAsync(
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<RunAngleRow>($"""
            SELECT angle.id AS "Id", angle.version AS "Version",
                jsonb_build_object(
                    'rank', angle.rank,
                    'title', angle.title,
                    'rationale', angle.rationale,
                    'evidence_item_ids', angle.evidence_item_ids_json,
                    'confidence', angle.confidence)::text AS "ArtifactJson"
            FROM commercial.opportunity_angles angle
            JOIN commercial.opportunity_angle_sets angle_set
              ON angle_set.tenant_id = angle.tenant_id AND angle_set.id = angle.angle_set_id
            WHERE angle.tenant_id = {tenantId.Value}
              AND angle_set.opportunity_id = {opportunityId}
              AND angle.status_code = {Gate4AngleStatuses.Selected}
            ORDER BY angle_set.version_no DESC
            LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);

    private Task<RunStrategyRow?> LoadRunStrategyAsync(
        TenantId tenantId,
        Guid runId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<RunStrategyRow>($"""
            SELECT id AS "Id", version_no AS "VersionNumber",
                artifact_json::text AS "ArtifactJson", version AS "Version"
            FROM commercial.strategy_versions
            WHERE tenant_id = {tenantId.Value} AND agent_run_id = {runId}
            """).SingleOrDefaultAsync(cancellationToken);

    private Task<bool> CriticExistsAsync(
        TenantId tenantId,
        Guid strategyId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.critic_reports
                WHERE tenant_id = {tenantId.Value}
                  AND strategy_version_id = {strategyId}) AS "Value"
            """).SingleAsync(cancellationToken);

    private static OpportunityAgentInput CreateInput(
        RunExecutionContext context,
        Guid stepId,
        string agentCode,
        IReadOnlyList<AgentPriorArtifactInput> priorArtifacts) => new(
        context.TenantId.Value,
        context.ActorId.Value,
        context.Run.Id,
        stepId,
        context.Run.CorrelationId,
        agentCode,
        context.Opportunity.Id,
        context.Opportunity.Title,
        context.Opportunity.ProblemSummary,
        context.Opportunity.ObjectiveSummary,
        context.Evidence[0].EvidenceSetId,
        context.Evidence[0].EvidenceSetVersion,
        context.Evidence.Select(ToEvidenceInput).ToArray(),
        priorArtifacts);

    private static AgentEvidenceInput ToEvidenceInput(ApprovedEvidenceRow row) => new(
        row.Id,
        row.ClaimType,
        ParseJson(row.StructuredValueJson),
        row.Excerpt);

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

internal sealed record RunExecutionContext(
    TenantId TenantId,
    ActorId ActorId,
    RunWorkRow Run,
    OpportunityRow Opportunity,
    IReadOnlyList<ApprovedEvidenceRow> Evidence,
    RunInterpretationRow? Interpretation,
    RunAngleRow? Angle,
    RunStrategyRow? Strategy,
    bool CriticExists);

internal sealed record RunInterpretationRow
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public string ArtifactJson { get; set; } = "{}";
    public long Version { get; set; }
}

internal sealed record RunAngleRow
{
    public Guid Id { get; set; }
    public string ArtifactJson { get; set; } = "{}";
    public long Version { get; set; }
}

internal sealed record RunStrategyRow
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public string ArtifactJson { get; set; } = "{}";
    public long Version { get; set; }
}
