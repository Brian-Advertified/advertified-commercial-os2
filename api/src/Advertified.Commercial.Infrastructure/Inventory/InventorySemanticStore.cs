using System.Text.Json;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventorySemanticStore(
    GovernanceDbContext dbContext,
    TimeProvider timeProvider)
{
    internal async Task<IReadOnlyList<
        InventorySemanticRunRow>> PrepareAsync(
        InventorySemanticContext context,
        Guid extractionAttemptId,
        IReadOnlyList<InventorySemanticPacket> packets,
        string modelCode,
        string promptVersion,
        string budgetScope,
        long budgetUsdMicros,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext,
            new UserId(context.ActorId),
            new TenantId(context.TenantId),
            cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({context.TenantId + ":" + budgetScope}, 0))",
            cancellationToken);

        var hashes = packets
            .Select(packet => packet.InputHash)
            .ToArray();
        var existing = await LoadAsync(
            context.TenantId,
            hashes,
            modelCode,
            promptVersion,
            cancellationToken);
        if (existing.Any(item =>
                item.Status is
                    MasterDataCodes.LifecycleStatuses.Running or
                    MasterDataCodes.LifecycleStatuses.ReviewRequired))
        {
            throw new InventorySemanticReconciliationRequiredException();
        }

        var existingHashes = existing
            .Select(item => item.InputHash)
            .ToHashSet(StringComparer.Ordinal);
        var missing = packets
            .Where(packet =>
                !existingHashes.Contains(packet.InputHash))
            .ToArray();
        var reserved = await dbContext.Database
            .SqlQuery<long>($"""
                SELECT COALESCE(sum(
                    CASE
                        WHEN status_code =
                            {MasterDataCodes.LifecycleStatuses.Completed}
                        THEN incremental_cost_usd_micros
                        ELSE maximum_cost_usd_micros
                    END), 0)::bigint AS "Value"
                FROM commercial.inventory_semantic_runs
                WHERE tenant_id = {context.TenantId}
                  AND budget_scope = {budgetScope}
                """)
            .SingleAsync(cancellationToken);
        var requested = missing.Sum(
            packet => packet.MaximumCostUsdMicros);
        if (requested > budgetUsdMicros - reserved)
            throw new InventorySemanticBudgetExceededException();

        var now = timeProvider.GetUtcNow();
        foreach (var packet in missing)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.inventory_semantic_runs (
                    id, tenant_id, import_id,
                    extraction_attempt_id, source_hash,
                    input_hash, budget_scope, prompt_version,
                    model_code, chunk_number, chunk_count,
                    status_code, request_json,
                    maximum_cost_usd_micros,
                    requested_by, created_at_utc)
                VALUES (
                    {Guid.NewGuid()}, {context.TenantId},
                    {context.ImportId}, {extractionAttemptId},
                    {context.SourceHash}, {packet.InputHash},
                    {budgetScope}, {promptVersion}, {modelCode},
                    {packet.Number}, {packet.Count},
                    {MasterDataCodes.LifecycleStatuses.Pending},
                    {packet.RequestJson}::jsonb,
                    {packet.MaximumCostUsdMicros},
                    {context.ActorId}, {now})
                """, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return await LoadForPacketsAsync(
            context,
            packets,
            modelCode,
            promptVersion,
            cancellationToken);
    }

    internal async Task MarkRunningAsync(
        InventorySemanticContext context,
        InventorySemanticRunRow run,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);
        await SetSessionAsync(context, cancellationToken);
        var changed = await dbContext.Database
            .ExecuteSqlInterpolatedAsync($"""
                UPDATE commercial.inventory_semantic_runs
                SET status_code =
                        {MasterDataCodes.LifecycleStatuses.Running},
                    started_at_utc = {timeProvider.GetUtcNow()},
                    version = version + 1
                WHERE tenant_id = {context.TenantId}
                  AND id = {run.Id}
                  AND status_code =
                        {MasterDataCodes.LifecycleStatuses.Pending}
                """, cancellationToken);
        if (changed != 1)
            throw new InventorySemanticReconciliationRequiredException();
        await transaction.CommitAsync(cancellationToken);
    }

    internal async Task MarkCompletedAsync(
        InventorySemanticContext context,
        InventorySemanticRunRow run,
        AgentRuntimeResponse<
            InventorySemanticExtractionArtifact> response,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(
            response, AgentRuntimeHttpSupport.WireJson);
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);
        await SetSessionAsync(context, cancellationToken);
        var changed = await dbContext.Database
            .ExecuteSqlInterpolatedAsync($"""
                UPDATE commercial.inventory_semantic_runs
                SET status_code =
                        {MasterDataCodes.LifecycleStatuses.Completed},
                    response_json = {json}::jsonb,
                    incremental_cost_usd_micros =
                        {response.Usage.IncrementalCostUsdMicros},
                    input_tokens = {response.Usage.InputTokens},
                    output_tokens = {response.Usage.OutputTokens},
                    provider_request_id =
                        {response.Usage.ProviderRequestId},
                    completed_at_utc = {timeProvider.GetUtcNow()},
                    version = version + 1
                WHERE tenant_id = {context.TenantId}
                  AND id = {run.Id}
                  AND status_code =
                        {MasterDataCodes.LifecycleStatuses.Running}
                  AND {response.Usage.IncrementalCostUsdMicros}
                        <= maximum_cost_usd_micros
                """, cancellationToken);
        if (changed != 1)
            throw new InventorySemanticReconciliationRequiredException();
        await transaction.CommitAsync(cancellationToken);
    }

    internal async Task MarkReconciliationRequiredAsync(
        InventorySemanticContext context,
        InventorySemanticRunRow run,
        string failureCode,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);
        await SetSessionAsync(context, cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_semantic_runs
            SET status_code =
                    {MasterDataCodes.LifecycleStatuses.ReviewRequired},
                failure_code = {failureCode},
                completed_at_utc = {timeProvider.GetUtcNow()},
                version = version + 1
            WHERE tenant_id = {context.TenantId}
              AND id = {run.Id}
              AND status_code =
                    {MasterDataCodes.LifecycleStatuses.Running}
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    internal async Task MarkRejectedAsync(
        InventorySemanticContext context,
        InventorySemanticRunRow run,
        AgentRuntimeRejectedException rejected,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);
        await SetSessionAsync(context, cancellationToken);
        var failureCode = "BEDROCK_" + rejected.Stage;
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_semantic_runs
            SET status_code =
                    {MasterDataCodes.LifecycleStatuses.Failed},
                response_json = {rejected.ResponseJson}::jsonb,
                incremental_cost_usd_micros =
                    {rejected.CostUsdMicros},
                input_tokens = {rejected.InputTokens},
                output_tokens = {rejected.OutputTokens},
                provider_request_id =
                    {rejected.ProviderRequestId},
                failure_code = {failureCode},
                completed_at_utc = {timeProvider.GetUtcNow()},
                version = version + 1
            WHERE tenant_id = {context.TenantId}
              AND id = {run.Id}
              AND status_code =
                    {MasterDataCodes.LifecycleStatuses.Running}
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    internal static AgentRuntimeResponse<
        InventorySemanticExtractionArtifact> ReadResponse(
        InventorySemanticRunRow row) =>
        JsonSerializer.Deserialize<AgentRuntimeResponse<
            InventorySemanticExtractionArtifact>>(
                row.ResponseJson ??
                    throw new InvalidOperationException(
                        "Semantic cache response is absent."),
                AgentRuntimeHttpSupport.WireJson)
        ?? throw new InvalidOperationException(
            "Semantic cache response is invalid.");

    private async Task<IReadOnlyList<
        InventorySemanticRunRow>> LoadForPacketsAsync(
        InventorySemanticContext context,
        IReadOnlyList<InventorySemanticPacket> packets,
        string modelCode,
        string promptVersion,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);
        await SetSessionAsync(context, cancellationToken);
        var rows = await LoadAsync(
            context.TenantId,
            packets.Select(packet => packet.InputHash).ToArray(),
            modelCode,
            promptVersion,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var byHash = rows.ToDictionary(
            item => item.InputHash, StringComparer.Ordinal);
        return packets.Select(packet => byHash[packet.InputHash])
            .ToArray();
    }

    private Task<List<InventorySemanticRunRow>> LoadAsync(
        Guid tenantId,
        string[] inputHashes,
        string modelCode,
        string promptVersion,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<
            InventorySemanticRunRow>($"""
                SELECT id AS "Id", input_hash AS "InputHash",
                    status_code AS "Status",
                    response_json::text AS "ResponseJson"
                FROM commercial.inventory_semantic_runs
                WHERE tenant_id = {tenantId}
                  AND input_hash = ANY({inputHashes})
                  AND model_code = {modelCode}
                  AND prompt_version = {promptVersion}
                """)
            .ToListAsync(cancellationToken);

    private Task SetSessionAsync(
        InventorySemanticContext context,
        CancellationToken cancellationToken) =>
        ApplicationDatabaseSession.SetAsync(
            dbContext,
            new UserId(context.ActorId),
            new TenantId(context.TenantId),
            cancellationToken);
}

internal sealed record InventorySemanticRunRow
{
    public Guid Id { get; set; }
    public string InputHash { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ResponseJson { get; set; }
}

internal sealed class InventorySemanticBudgetExceededException :
    Exception
{
}

internal sealed class
    InventorySemanticReconciliationRequiredException :
    Exception
{
}

internal sealed class InventorySemanticResultRejectedException(
    string stage) : Exception(stage)
{
}
