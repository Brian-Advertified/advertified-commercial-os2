using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Persistence;

// A prepared external side effect reserves the same identity as command completion.
// The reservation is retained even when acceptance is unknown or the HTTP request ends.
internal sealed record CommandIntentIdentity(
    TenantId TenantId, IdempotencyKey Key, Sha256Digest Hash,
    CommandId CommandId, CorrelationId CorrelationId)
{
    internal static CommandIntentIdentity From<T>(CommandEnvelope<T> envelope) where T : notnull =>
        new(envelope.TenantId, envelope.IdempotencyKey, envelope.PayloadHash,
            envelope.CommandId, envelope.CorrelationId);

    internal Task<int> LockAsync(GovernanceDbContext db, CancellationToken cancellationToken)
    {
        var key = $"{TenantId.Value:N}:{Key.Value}";
        return db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }

    internal async Task EnsureCompatibleAsync(GovernanceDbContext db, CancellationToken cancellationToken)
    {
        var retainedHash = await db.Database.SqlQuery<string>($"""
            SELECT request_hash AS "Value" FROM commercial.command_intents
            WHERE tenant_id = {TenantId.Value} AND idempotency_key = {Key.Value}
            """).SingleOrDefaultAsync(cancellationToken);
        if (retainedHash is not null && retainedHash != Hash.Value)
            throw new IdempotencyConflictException();
        var completed = await db.IdempotencyRecords.SingleOrDefaultAsync(
            row => row.TenantId == TenantId && row.Key == Key, cancellationToken);
        if (completed is not null && completed.RequestHash != Hash)
            throw new IdempotencyConflictException();
    }

    internal async Task ReserveAsync(GovernanceDbContext db, ActorId actor, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await LockAsync(db, cancellationToken);
        await EnsureCompatibleAsync(db, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.command_intents (
                tenant_id, idempotency_key, request_hash, command_id, correlation_id, actor_id, created_at_utc)
            VALUES ({TenantId.Value}, {Key.Value}, {Hash.Value}, {CommandId.Value},
                {CorrelationId.Value}, {actor.Value}, {now})
            ON CONFLICT (tenant_id, idempotency_key) DO NOTHING
            """, cancellationToken);
    }
}
