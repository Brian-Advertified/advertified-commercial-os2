using System.Data;
using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Persistence;

public sealed class PersistedCommandUnitOfWork(
    GovernanceDbContext dbContext,
    TimeProvider timeProvider) : IIdempotentCommandUnitOfWork
{
    private static readonly TimeSpan RecordLifetime = TimeSpan.FromHours(24);
    private const string EmptyMetadata = "{}";
    private const string DuplicateAction = "command.duplicate_received";

    public async Task<CommandReceipt> ExecuteOnceAsync<TCommand>(
        CommandEnvelope<TCommand> envelope,
        Func<CancellationToken, Task<CommandOutcome>> handler,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext,
            new UserId(envelope.ActorId.Value),
            envelope.TenantId,
            cancellationToken);
        await AcquireCommandLockAsync(envelope, cancellationToken);

        var stored = await dbContext.IdempotencyRecords.SingleOrDefaultAsync(
            item => item.TenantId == envelope.TenantId
                && item.Key == envelope.IdempotencyKey,
            cancellationToken);

        if (stored is not null)
        {
            var receipt = await ReplayAsync(stored, envelope, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return receipt;
        }

        var outcome = await handler(cancellationToken);
        PersistApplied(envelope, outcome);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CommandReceipt(CommandDisposition.Applied, outcome, outcome.Audit);
    }

    private async Task<CommandReceipt> ReplayAsync<TCommand>(
        IdempotencyRecordRow stored,
        CommandEnvelope<TCommand> envelope,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        if (stored.RequestHash != envelope.PayloadHash)
        {
            throw new IdempotencyConflictException();
        }

        var storedOutcome = JsonSerializer.Deserialize<StoredCommandOutcome>(stored.OutcomeJson)
            ?? throw new InvalidOperationException("The stored command result is unavailable.");
        var outcome = storedOutcome.ToDomain();
        var replayAudit = new AuditRecord(
            Guid.NewGuid(),
            envelope.TenantId,
            envelope.ActorId,
            envelope.CommandId,
            envelope.CorrelationId,
            new ActionCode(DuplicateAction),
            outcome.Audit.Resource,
            timeProvider.GetUtcNow());

        dbContext.AuditEvents.Add(new AuditEventRow(replayAudit, EmptyMetadata));
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CommandReceipt(CommandDisposition.Replayed, outcome, replayAudit);
    }

    private void PersistApplied<TCommand>(
        CommandEnvelope<TCommand> envelope,
        CommandOutcome outcome)
        where TCommand : notnull
    {
        var now = timeProvider.GetUtcNow();
        var outcomeJson = JsonSerializer.Serialize(StoredCommandOutcome.FromDomain(outcome));
        dbContext.IdempotencyRecords.Add(new IdempotencyRecordRow(
            envelope.TenantId,
            envelope.IdempotencyKey,
            envelope.CommandId,
            envelope.PayloadHash,
            outcomeJson,
            now,
            now.Add(RecordLifetime)));
        dbContext.AuditEvents.Add(new AuditEventRow(outcome.Audit, EmptyMetadata));
        dbContext.OutboxMessages.Add(new OutboxMessageRow(outcome.Outbox));
    }

    private Task<int> AcquireCommandLockAsync<TCommand>(
        CommandEnvelope<TCommand> envelope,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var lockKey = $"{envelope.TenantId.Value:N}:{envelope.IdempotencyKey.Value}";
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
    }
}
