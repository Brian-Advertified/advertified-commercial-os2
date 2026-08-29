using System.Text.Json;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Infrastructure.Foundation;

internal static class CommandOutcomeFactory
{
    public static CommandOutcome Create<TCommand, TResult>(
        CommandEnvelope<TCommand> envelope,
        TResult data,
        Guid resourceId,
        long version,
        ResourceTypeCode resourceType,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset occurredAtUtc)
        where TCommand : notnull
        where TResult : notnull
    {
        var payload = JsonSerializer.SerializeToElement(data);
        var resource = new ResourceReference(resourceType, resourceId, version);
        return new CommandOutcome(
            payload,
            version,
            new AuditRecord(
                Guid.NewGuid(),
                envelope.TenantId,
                envelope.ActorId,
                envelope.CommandId,
                envelope.CorrelationId,
                action,
                resource,
                occurredAtUtc),
            new OutboxMessage(
                Guid.NewGuid(),
                envelope.TenantId,
                envelope.CommandId,
                envelope.CorrelationId,
                eventType,
                resource,
                payload,
                occurredAtUtc));
    }

    public static CommandResult<TResult> ToResult<TResult>(CommandReceipt receipt)
        where TResult : notnull
    {
        var data = receipt.Outcome.Data.Deserialize<TResult>()
            ?? throw new InvalidOperationException("The stored command result is unavailable.");
        return new CommandResult<TResult>(
            data,
            receipt.Outcome.AggregateVersion,
            receipt.Disposition == CommandDisposition.Replayed);
    }
}
