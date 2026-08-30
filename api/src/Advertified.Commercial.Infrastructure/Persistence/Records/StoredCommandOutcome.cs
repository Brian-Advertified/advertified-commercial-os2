using System.Text.Json;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Infrastructure.Persistence.Records;

internal sealed record StoredCommandOutcome(
    JsonElement Data,
    long AggregateVersion,
    StoredAuditRecord Audit,
    StoredOutboxMessage Outbox,
    IReadOnlyList<StoredAuditRecord>? AdditionalAudits = null,
    IReadOnlyList<StoredOutboxMessage>? AdditionalOutbox = null)
{
    public static StoredCommandOutcome FromDomain(CommandOutcome outcome)
    {
        return new StoredCommandOutcome(
            outcome.Data.Clone(),
            outcome.AggregateVersion,
            StoredAuditRecord.FromDomain(outcome.Audit),
            StoredOutboxMessage.FromDomain(outcome.Outbox),
            outcome.AdditionalAudits.Select(StoredAuditRecord.FromDomain).ToArray(),
            outcome.AdditionalOutbox.Select(StoredOutboxMessage.FromDomain).ToArray());
    }

    public CommandOutcome ToDomain()
    {
        return new CommandOutcome(
            Data.Clone(),
            AggregateVersion,
            Audit.ToDomain(),
            Outbox.ToDomain(),
            AdditionalAudits?.Select(item => item.ToDomain()),
            AdditionalOutbox?.Select(item => item.ToDomain()));
    }
}

internal sealed record StoredResourceReference(
    string Type,
    Guid Id,
    long Version)
{
    public static StoredResourceReference FromDomain(ResourceReference resource)
    {
        return new StoredResourceReference(
            resource.ResourceType.Value,
            resource.ResourceId,
            resource.Version);
    }

    public ResourceReference ToDomain()
    {
        return new ResourceReference(new ResourceTypeCode(Type), Id, Version);
    }
}

internal sealed record StoredAuditRecord(
    Guid Id,
    Guid TenantId,
    Guid ActorId,
    Guid CommandId,
    Guid CorrelationId,
    string Action,
    StoredResourceReference Resource,
    DateTimeOffset OccurredAtUtc)
{
    public static StoredAuditRecord FromDomain(AuditRecord audit)
    {
        return new StoredAuditRecord(
            audit.AuditId,
            audit.TenantId.Value,
            audit.ActorId.Value,
            audit.CommandId.Value,
            audit.CorrelationId.Value,
            audit.Action.Value,
            StoredResourceReference.FromDomain(audit.Resource),
            audit.OccurredAtUtc);
    }

    public AuditRecord ToDomain()
    {
        return new AuditRecord(
            Id,
            new TenantId(TenantId),
            new ActorId(ActorId),
            new CommandId(CommandId),
            new CorrelationId(CorrelationId),
            new ActionCode(Action),
            Resource.ToDomain(),
            OccurredAtUtc);
    }
}

internal sealed record StoredOutboxMessage(
    Guid Id,
    Guid TenantId,
    Guid CausationId,
    Guid CorrelationId,
    string EventType,
    StoredResourceReference Aggregate,
    JsonElement Payload,
    DateTimeOffset OccurredAtUtc)
{
    public static StoredOutboxMessage FromDomain(OutboxMessage message)
    {
        return new StoredOutboxMessage(
            message.EventId,
            message.TenantId.Value,
            message.CausationId.Value,
            message.CorrelationId.Value,
            message.EventType.Value,
            StoredResourceReference.FromDomain(message.Aggregate),
            message.Payload.Clone(),
            message.OccurredAtUtc);
    }

    public OutboxMessage ToDomain()
    {
        return new OutboxMessage(
            Id,
            new TenantId(TenantId),
            new CommandId(CausationId),
            new CorrelationId(CorrelationId),
            new EventTypeCode(EventType),
            Aggregate.ToDomain(),
            Payload.Clone(),
            OccurredAtUtc);
    }
}
