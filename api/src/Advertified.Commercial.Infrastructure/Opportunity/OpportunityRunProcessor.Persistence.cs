using System.Text.Json;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityRunProcessor
{
    private static readonly TimeSpan[] RetryIntervals =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
    ];
    private static readonly JsonSerializerOptions StoredOutputJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private async Task<Guid> PrepareStepAsync(
        RunExecutionContext context,
        string stepCode,
        string agentCode,
        string inputHash,
        CancellationToken cancellationToken)
    {
        await using var transaction = await runStore.BeginSessionAsync(
            context.ActorId, context.TenantId, cancellationToken);
        var proposedId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.agent_run_steps (
                id, tenant_id, run_id, step_code, agent_code, status_code,
                input_hash, attempt_count, created_at_utc, updated_at_utc)
            VALUES (
                {proposedId}, {context.TenantId.Value}, {context.Run.Id}, {stepCode},
                {agentCode}, {Gate4Statuses.Running}, {inputHash}, 1, {now}, {now})
            ON CONFLICT (tenant_id, run_id, step_code) DO UPDATE
            SET status_code = {Gate4Statuses.Running},
                input_hash = EXCLUDED.input_hash,
                attempt_count = commercial.agent_run_steps.attempt_count + 1,
                updated_at_utc = EXCLUDED.updated_at_utc
            """, cancellationToken);
        var stepId = await store.DbContext.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM commercial.agent_run_steps
            WHERE tenant_id = {context.TenantId.Value} AND run_id = {context.Run.Id}
              AND step_code = {stepCode}
            """).SingleAsync(cancellationToken);
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.agent_runs
            SET current_step_code = {stepCode}, updated_at_utc = {now}
            WHERE tenant_id = {context.TenantId.Value} AND id = {context.Run.Id}
              AND status_code = {Gate4Statuses.Running}
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return stepId;
    }

    private async Task PersistSuccessfulStepAsync(
        RunExecutionContext context,
        AgentStepExecution execution,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var outputJson = JsonSerializer.Serialize(execution.Output, StoredOutputJson);
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.agent_run_steps
            SET status_code = {Gate4Statuses.Completed}, output_json = {outputJson}::jsonb,
                checkpointed_at_utc = {now}, updated_at_utc = {now}
            WHERE tenant_id = {context.TenantId.Value} AND id = {execution.StepId}
              AND run_id = {context.Run.Id}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new InvalidOperationException("The durable run step was not available.");
        }

        var usage = execution.Output.Usage;
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.ai_usage_ledger (
                id, tenant_id, run_id, step_id, provider_code, model_code, units,
                tool_calls, incremental_cost_minor, cache_status_code, recorded_at_utc)
            VALUES (
                {Guid.NewGuid()}, {context.TenantId.Value}, {context.Run.Id},
                {execution.StepId}, {usage.Provider}, {usage.Model}, {usage.Units},
                {usage.ToolCalls}, {usage.IncrementalCostMinor}, {usage.CacheStatus}, {now})
            """, cancellationToken);
    }

    private async Task CompleteRunAsync(
        RunExecutionContext context,
        string stepCode,
        CancellationToken cancellationToken)
    {
        await using var transaction = await runStore.BeginSessionAsync(
            context.ActorId, context.TenantId, cancellationToken);
        await CompleteRunCoreAsync(context, stepCode, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task<int> CompleteRunCoreAsync(
        RunExecutionContext context,
        string stepCode,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.agent_runs
            SET status_code = {Gate4Statuses.Completed}, current_step_code = {stepCode},
                lease_owner = NULL, lease_expires_at_utc = NULL,
                error_code = NULL, error_detail = NULL,
                completed_at_utc = {now}, updated_at_utc = {now}, version = version + 1
            WHERE tenant_id = {context.TenantId.Value} AND id = {context.Run.Id}
            """, cancellationToken);
    }

    private async Task RecordFailureAsync(
        RunClaim claim,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var tenantId = new TenantId(claim.TenantId);
        var actorId = new ActorId(claim.RequestedBy);
        await using var transaction = await runStore.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var run = await runStore.FindWorkAsync(tenantId, claim.RunId, cancellationToken);
        if (run is null || run.Status == Gate4Statuses.Cancelled)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var (code, detail) = SafeFailure(exception);
        var now = timeProvider.GetUtcNow();
        var retryDelay = RetryDelay(exception, run.Attempts);
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.agent_run_steps
            SET status_code = {Gate4Statuses.Failed}, updated_at_utc = {now}
            WHERE tenant_id = {tenantId.Value} AND run_id = {claim.RunId}
              AND status_code = {Gate4Statuses.Running}
            """, cancellationToken);
        var status = retryDelay.HasValue ? Gate4Statuses.Queued : Gate4Statuses.ReviewRequired;
        var nextAttempt = retryDelay.HasValue ? now.Add(retryDelay.Value) : (DateTimeOffset?)null;
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.agent_runs
            SET status_code = {status}, error_code = {code},
                error_detail = {detail}, lease_owner = NULL, lease_expires_at_utc = NULL,
                next_attempt_at_utc = {nextAttempt}, updated_at_utc = {now}, version = version + 1
            WHERE tenant_id = {tenantId.Value} AND id = {claim.RunId}
            """, cancellationToken);
        if (!retryDelay.HasValue)
        {
            await CreateRecoveryTaskAsync(run, tenantId, code, now, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task CreateRecoveryTaskAsync(
        RunWorkRow run,
        TenantId tenantId,
        string code,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var exists = await store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.human_tasks
                WHERE tenant_id = {tenantId.Value} AND resource_id = {run.Id}
                  AND task_type_code = {Gate4TaskTypes.RunRecovery}
                  AND status_code = {Gate4Statuses.Pending}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!exists)
        {
            await OpportunityCommandSupport.CreateTaskAsync(
                store.DbContext,
                tenantId,
                run.OpportunityId,
                Gate4TaskTypes.RunRecovery,
                "Review the paused agent run",
                $"The run stopped safely with code {code}.",
                CommercialResourceTypes.AgentRun,
                run.Id,
                run.Version + 1,
                run.RequestedBy,
                now,
                cancellationToken);
        }
    }

    private static (string Code, string Detail) SafeFailure(Exception exception) => exception switch
    {
        RunInputVersionDriftException => (
            "INPUT_VERSION_DRIFT",
            "The opportunity changed after this run was queued."),
        EvidenceRequiredException => (
            "EVIDENCE_REQUIRED",
            "Approved evidence is required before this run can continue."),
        HttpRequestException => (
            "AGENT_RUNTIME_UNAVAILABLE",
            "The deterministic agent runtime was unavailable."),
        _ => (
            "AGENT_OUTPUT_INVALID",
            "The run stopped because its deterministic output did not pass validation."),
    };

    private static TimeSpan? RetryDelay(Exception exception, int attempts)
    {
        if (exception is not HttpRequestException || attempts <= 0 || attempts > RetryIntervals.Length)
        {
            return null;
        }
        return RetryIntervals[attempts - 1];
    }
}
