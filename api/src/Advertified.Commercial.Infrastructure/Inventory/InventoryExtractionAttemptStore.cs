using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryExtractionAttemptStore(
    GovernanceDbContext dbContext,
    InventoryRecordStore inventoryStore,
    TimeProvider timeProvider)
{
    internal GovernanceDbContext DbContext => dbContext;
    internal DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    internal async Task<Guid> QueueInitialAsync<TCommand>(
        InventoryImportRow source,
        CommandEnvelope<TCommand> envelope,
        IDurableInventoryDocumentExtractionAdapter provider,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var attemptId = Guid.NewGuid();
        var now = UtcNow;
        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_extraction_attempts (
                id, tenant_id, import_id, source_file_version, source_hash,
                stable_submission_key, provider_name, provider_version, status_code,
                polling_checkpoint, attempt_number, correlation_id, command_id,
                requested_by, version, created_at_utc, updated_at_utc)
            SELECT {attemptId}, {envelope.TenantId.Value}, {source.Id}, {source.Version},
                {source.SourceHash}, {envelope.IdempotencyKey.Value}, {provider.ProviderName},
                {provider.ProviderVersion},
                {MasterDataCodes.InventoryExtractionAttemptStatuses.Pending},
                {"{}"}::jsonb, 1, {envelope.CorrelationId.Value},
                {envelope.CommandId.Value}, {envelope.ActorId.Value}, 1, {now}, {now}
            WHERE NOT EXISTS (
                SELECT 1 FROM commercial.inventory_extraction_attempts attempt
                WHERE attempt.tenant_id = {envelope.TenantId.Value}
                  AND attempt.import_id = {source.Id})
            """, cancellationToken);
        if (inserted != 1)
        {
            throw new InvalidLifecycleTransitionException();
        }
        await MarkImportQueuedAsync(source, envelope.TenantId, now, cancellationToken);
        return attemptId;
    }

    internal async Task<InventoryExtractionSource> ReadSourceAsync(
        InventoryExtractionWorkerClaim claim,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSessionAsync(claim, cancellationToken);
        var source = await inventoryStore.FindImportAsync(
            new TenantId(claim.TenantId), claim.ImportId, false, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory import access denied.");
        await transaction.CommitAsync(cancellationToken);
        if (source.ProtectedObjectKey is null || source.DocumentClass is null ||
            !string.Equals(source.SourceHash, claim.SourceHash, StringComparison.Ordinal))
        {
            throw new InventoryProtectionUnavailableException();
        }
        var content = await inventoryStore.ObjectStore.ReadAsync(
            source.ProtectedObjectKey, cancellationToken);
        return new InventoryExtractionSource(
            source, new InventoryExtractionRequest(
                source.FileName, source.DeclaredMediaType, source.DocumentClass,
                source.SourceHash, content));
    }

    internal async Task<IDbContextTransaction> BeginSessionAsync(
        InventoryExtractionWorkerClaim claim,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(claim.RequestedBy), new TenantId(claim.TenantId),
            cancellationToken);
        return transaction;
    }

    private async Task MarkImportQueuedAsync(
        InventoryImportRow source,
        TenantId tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_imports
            SET status_code = {MasterDataCodes.LifecycleStatuses.Extracting},
                version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {tenantId.Value} AND id = {source.Id}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Uploaded}
              AND version = {source.Version}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_import_steps (
                id, tenant_id, import_id, step_type_code, status_code,
                outcome_json, started_at_utc, completed_at_utc)
            VALUES ({Guid.NewGuid()}, {tenantId.Value}, {source.Id},
                {MasterDataCodes.InventoryImportStepTypes.Extraction},
                {MasterDataCodes.LifecycleStatuses.Running}, {"{}"}::jsonb, {now}, NULL)
            ON CONFLICT (tenant_id, import_id, step_type_code) DO UPDATE
            SET status_code = EXCLUDED.status_code,
                started_at_utc = EXCLUDED.started_at_utc,
                completed_at_utc = NULL
            """, cancellationToken);
    }
}

internal sealed record InventoryExtractionSource(
    InventoryImportRow Import,
    InventoryExtractionRequest Request);
