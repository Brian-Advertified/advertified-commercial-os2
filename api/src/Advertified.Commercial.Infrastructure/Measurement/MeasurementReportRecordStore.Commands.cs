using System.Text.Json;
using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Measurement;

public sealed partial class MeasurementReportRecordStore
{
    internal async Task InsertTraceAndReportAsync(
        PreparedMeasurementReport report,
        MeasurementReportSourceRow source,
        CommandEnvelope<GenerateMeasurementReportCommand> envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await InsertRunAsync(report, source, envelope, now, cancellationToken);
        await InsertStepAndUsageAsync(report, source, now, cancellationToken);
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.measurement_report_versions (
                id, tenant_id, campaign_id, version_no, agent_run_id, campaign_version,
                measurement_plan_json, evidence_versions_json, metric_ids,
                interpretation_json, limitations_json, input_hash, output_hash,
                agent_contract_version, prompt_version, provider_code, model_code,
                tool_calls, incremental_cost_minor, provider_request_id,
                output_validated, status_code,
                approver_user_id, generated_by, generated_at_utc, version, updated_at_utc)
            SELECT {report.ReportId}, {source.TenantId}, {source.CampaignId},
                COALESCE(max(existing.version_no), 0) + 1, {report.RunId},
                {source.CampaignVersion}, {source.MeasurementPlanJson}::jsonb,
                {report.EvidenceVersionsJson}::jsonb, {report.MetricIds},
                {report.InterpretationJson}::jsonb, {report.LimitationsJson}::jsonb,
                {report.InputHash}, {report.OutputHash}, {report.Proposal.ContractVersion},
                {report.Proposal.PromptVersion}, {report.Proposal.Provider},
                {report.Proposal.Model}, {report.Proposal.ToolCalls},
                {report.Proposal.IncrementalCostMinor}, {report.Proposal.ProviderRequestId}, true,
                {MasterDataCodes.LifecycleStatuses.ReviewRequired},
                {source.ApproverUserId}, {envelope.ActorId.Value}, {now}, 1, {now}
            FROM commercial.measurement_report_versions existing
            WHERE existing.tenant_id = {source.TenantId}
              AND existing.campaign_id = {source.CampaignId}
            """, cancellationToken);
        if (changed != 1) throw new MeasurementReportBlockedException();
    }

    internal async Task ReviewAsync(
        MeasurementReportRow report,
        CommandEnvelope<ReviewMeasurementReportCommand> envelope,
        string decision,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.measurement_report_versions
            SET status_code = {decision}, reviewed_by = {envelope.ActorId.Value},
                reviewed_at_utc = {now}, review_reason = {reason},
                version = version + 1, updated_at_utc = {now}
            WHERE id = {report.Id} AND tenant_id = {envelope.TenantId.Value}
              AND campaign_id = {report.CampaignId}
              AND approver_user_id = {envelope.ActorId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.ReviewRequired}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }

    private Task<int> InsertRunAsync(
        PreparedMeasurementReport report,
        MeasurementReportSourceRow source,
        CommandEnvelope<GenerateMeasurementReportCommand> envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.agent_runs (
                id, tenant_id, opportunity_id, campaign_id, run_kind_code,
                status_code, input_version, requested_by, approver_user_id,
                correlation_id, current_step_code, attempts, version,
                created_at_utc, updated_at_utc, completed_at_utc)
            VALUES ({report.RunId}, {source.TenantId}, {source.OpportunityId},
                {source.CampaignId}, {MasterDataCodes.AgentRunKinds.Measurement},
                {MasterDataCodes.LifecycleStatuses.Completed}, {source.CampaignVersion},
                {envelope.ActorId.Value}, {source.ApproverUserId},
                {envelope.CorrelationId.Value}, {MasterDataCodes.WorkflowStepTypes.Measurement},
                1, 1, {now}, {now}, {now})
            """, cancellationToken);

    private async Task InsertStepAndUsageAsync(
        PreparedMeasurementReport report,
        MeasurementReportSourceRow source,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.agent_run_steps (
                id, tenant_id, run_id, step_code, agent_code, status_code,
                input_hash, output_json, attempt_count, checkpointed_at_utc,
                created_at_utc, updated_at_utc)
            VALUES ({report.StepId}, {source.TenantId}, {report.RunId},
                {MasterDataCodes.WorkflowStepTypes.Measurement},
                {MasterDataCodes.AgentTypes.Measurement},
                {MasterDataCodes.LifecycleStatuses.Completed}, {report.InputHash},
                {report.TraceOutputJson}::jsonb, 1, {now}, {now}, {now})
            """, cancellationToken);
        await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.ai_usage_ledger (
                id, tenant_id, run_id, step_id, provider_code, model_code,
                units, tool_calls, incremental_cost_minor, cache_status_code,
                provider_request_id, recorded_at_utc)
            VALUES ({Guid.NewGuid()}, {source.TenantId}, {report.RunId}, {report.StepId},
                {report.Proposal.Provider}, {report.Proposal.Model},
                {report.Proposal.Units}, {report.Proposal.ToolCalls},
                {report.Proposal.IncrementalCostMinor}, {report.Proposal.CacheStatus},
                {report.Proposal.ProviderRequestId}, {now})
            """, cancellationToken);
    }
}

internal sealed record PreparedMeasurementReport(
    Guid ReportId,
    Guid RunId,
    Guid StepId,
    Guid[] MetricIds,
    string EvidenceVersionsJson,
    string InterpretationJson,
    string TraceOutputJson,
    string LimitationsJson,
    string InputHash,
    string OutputHash,
    MeasurementAgentProposal Proposal);
