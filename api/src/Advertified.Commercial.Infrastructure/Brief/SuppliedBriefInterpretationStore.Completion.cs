using System.Text.Json;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Brief;

public sealed partial class SuppliedBriefInterpretationStore
{
    public async Task RejectAsync(SuppliedBriefAgentInput input, SuppliedBriefValidationException failure,
        CancellationToken cancellationToken)
    {
        await using var transaction = await store.BeginSessionAsync(new ActorId(input.ActorId),
            new TenantId(input.TenantId), cancellationToken);
        await EnsureReservedAsync(input, cancellationToken);
        var output = JsonSerializer.Serialize(new { RejectedResponse = failure.ResponseJson,
            failure.Usage, ValidationError = failure.Message }, JsonOptions);
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.agent_run_steps SET status_code = {MasterDataCodes.LifecycleStatuses.Failed},
                output_json = {output}::jsonb, checkpointed_at_utc = {timeProvider.GetUtcNow()},
                updated_at_utc = {timeProvider.GetUtcNow()}
            WHERE tenant_id = {input.TenantId} AND run_id = {input.Interpretation!.Id}
                AND status_code = {MasterDataCodes.LifecycleStatuses.Running} AND output_json IS NULL
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        await InsertUsageAsync(input, failure.Usage, cancellationToken);
        await SetStatusAsync(input, MasterDataCodes.LifecycleStatuses.Failed, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task CompleteAsync(SuppliedBriefAgentInput input, SuppliedBriefUnderstandingView result,
        CancellationToken cancellationToken)
    {
        var id = input.Interpretation?.Id ?? throw new InvalidOperationException("Retained input is required.");
        await using var transaction = await store.BeginSessionAsync(new ActorId(input.ActorId),
            new TenantId(input.TenantId), cancellationToken);
        await EnsureReservedAsync(input, cancellationToken);
        if (result.Interpretation != input.Interpretation) throw new VersionConflictException();
        var now = timeProvider.GetUtcNow();
        var output = JsonSerializer.Serialize(result, JsonOptions);
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.agent_run_steps SET status_code = {MasterDataCodes.LifecycleStatuses.Completed},
                output_json = {output}::jsonb, checkpointed_at_utc = {now}, updated_at_utc = {now}
            WHERE tenant_id = {input.TenantId} AND run_id = {id} AND id = {id}
                AND status_code = {MasterDataCodes.LifecycleStatuses.Running} AND output_json IS NULL
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        await InsertUsageAsync(input, result.Usage, cancellationToken);
        await SetStatusAsync(input, MasterDataCodes.LifecycleStatuses.Completed, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task FailAsync(SuppliedBriefAgentInput input, CancellationToken cancellationToken)
    {
        await using var transaction = await store.BeginSessionAsync(new ActorId(input.ActorId),
            new TenantId(input.TenantId), cancellationToken);
        await EnsureReservedAsync(input, cancellationToken);
        await SetStatusAsync(input, MasterDataCodes.LifecycleStatuses.Failed, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsureReservedAsync(SuppliedBriefAgentInput input, CancellationToken cancellationToken)
    {
        var reference = input.Interpretation ?? throw new InvalidOperationException("Retained input is required.");
        var source = await FindAsync(input, reference.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("Supplied Brief interpretation access denied.");
        if (source.SourceHash != reference.SourceHash || source.ParentId != reference.ParentId ||
            source.Version != reference.Version)
            throw new VersionConflictException();
    }

    private Task<int> InsertUsageAsync(SuppliedBriefAgentInput input, SuppliedBriefAgentUsageView usage,
        CancellationToken cancellationToken) => store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO commercial.ai_usage_ledger (id, tenant_id, run_id, step_id, provider_code,
            model_code, units, tool_calls, incremental_cost_minor, cache_status_code, provider_request_id, recorded_at_utc)
        VALUES ({Guid.NewGuid()}, {input.TenantId}, {input.Interpretation!.Id}, {input.Interpretation.Id},
            {usage.Provider}, {usage.Model}, {usage.Units}, {usage.ToolCalls}, {usage.IncrementalCostMinor},
            {usage.CacheStatus ?? AgentProviderMetadata.FixtureCacheStatus}, {usage.ProviderRequestId}, {timeProvider.GetUtcNow()})
        """, cancellationToken);

    private Task<int> SetStatusAsync(SuppliedBriefAgentInput input, string status,
        CancellationToken cancellationToken) => store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE commercial.agent_runs SET status_code = {status}, completed_at_utc = {timeProvider.GetUtcNow()},
            updated_at_utc = {timeProvider.GetUtcNow()}, version = version + 1
        WHERE tenant_id = {input.TenantId} AND id = {input.Interpretation!.Id} AND requested_by = {input.ActorId}
            AND status_code = {MasterDataCodes.LifecycleStatuses.Running};
        UPDATE commercial.agent_run_steps SET status_code = {status}, updated_at_utc = {timeProvider.GetUtcNow()}
        WHERE tenant_id = {input.TenantId} AND run_id = {input.Interpretation.Id}
            AND status_code = {MasterDataCodes.LifecycleStatuses.Running};
        """, cancellationToken);
}
