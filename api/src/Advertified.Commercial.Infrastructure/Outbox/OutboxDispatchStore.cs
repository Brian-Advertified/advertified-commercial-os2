using System.Text.Json;
using Advertified.Commercial.Application.Outbox;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Outbox;

public sealed class OutboxDispatchStore(GovernanceDbContext dbContext)
{
    public async Task<OutboxDispatchSelection?> ClaimNextAsync(
        Guid tenantId,
        Guid workerId,
        int leaseSeconds,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext,
            null,
            new TenantId(tenantId),
            cancellationToken);
        var row = await dbContext.Database.SqlQuery<OutboxDispatchClaimRow>($"""
            SELECT event_id AS "EventId", tenant_id AS "TenantId",
                causation_id AS "CausationId", correlation_id AS "CorrelationId",
                event_type_code AS "EventType", aggregate_type_code AS "AggregateType",
                aggregate_id AS "AggregateId", aggregate_version AS "AggregateVersion",
                payload_text AS "PayloadJson", occurred_at_utc AS "OccurredAtUtc",
                claim_token AS "ClaimToken", attempts AS "Attempt",
                lease_expires_at_utc AS "LeaseExpiresAtUtc",
                dead_lettered_on_claim AS "DeadLetteredOnClaim",
                failure_code AS "FailureCode"
            FROM commercial.claim_next_outbox_event(
                {workerId}, {leaseSeconds})
            """).SingleOrDefaultAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return row is null ? null : Map(row);
    }

    public Task<bool> HeartbeatAsync(
        Guid tenantId,
        Guid eventId,
        Guid claimToken,
        int leaseSeconds,
        CancellationToken cancellationToken) => ExecuteTransitionAsync(
            $"""
            SELECT commercial.heartbeat_outbox_event(
                {eventId}, {claimToken}, {leaseSeconds}) AS "Value"
            """,
            tenantId,
            cancellationToken);

    public Task<bool> AcknowledgeAsync(
        Guid tenantId,
        Guid eventId,
        Guid claimToken,
        string transportReference,
        CancellationToken cancellationToken) => ExecuteTransitionAsync(
            $"""
            SELECT commercial.acknowledge_outbox_event(
                {eventId}, {claimToken}, {transportReference}) AS "Value"
            """,
            tenantId,
            cancellationToken);

    public Task<bool> FailAsync(
        Guid tenantId,
        Guid eventId,
        Guid claimToken,
        bool terminal,
        string failureCode,
        CancellationToken cancellationToken) => ExecuteTransitionAsync(
            $"""
            SELECT commercial.fail_outbox_event(
                {eventId}, {claimToken}, {terminal}, {failureCode}) AS "Value"
            """,
            tenantId,
            cancellationToken);

    private async Task<bool> ExecuteTransitionAsync(
        FormattableString query,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext,
            null,
            new TenantId(tenantId),
            cancellationToken);
        var changed = await dbContext.Database.SqlQuery<bool>(query)
            .SingleAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return changed;
    }

    private static OutboxDispatchSelection Map(OutboxDispatchClaimRow row)
    {
        using var document = JsonDocument.Parse(row.PayloadJson);
        var envelope = new OutboxDeliveryEnvelope(
            row.EventId,
            row.TenantId,
            row.CausationId,
            row.CorrelationId,
            row.EventType,
            row.AggregateType,
            row.AggregateId,
            row.AggregateVersion,
            document.RootElement.Clone(),
            row.OccurredAtUtc);
        if (row.DeadLetteredOnClaim)
        {
            return new OutboxDispatchSelection(
                null,
                new OutboxDispatchDeadLetter(
                    row.EventId,
                    row.CorrelationId,
                    row.Attempt,
                    row.FailureCode ?? throw InvalidDispatchState()));
        }

        var claim = new OutboxDispatchClaim(
            envelope,
            row.ClaimToken ?? throw InvalidDispatchState(),
            row.Attempt,
            row.LeaseExpiresAtUtc ?? throw InvalidDispatchState());
        return new OutboxDispatchSelection(claim, null);
    }

    private static InvalidOperationException InvalidDispatchState() => new(
        "The database returned an invalid outbox dispatch transition.");
}
