using System.Text.Json;

namespace Advertified.Commercial.Domain.Governance;

public sealed record ResourceReference
{
    public ResourceReference(
        ResourceTypeCode resourceType,
        Guid resourceId,
        long version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType.Value);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);

        ResourceType = resourceType;
        ResourceId = IdValue.Require(resourceId, nameof(resourceId));
        Version = version;
    }

    public ResourceTypeCode ResourceType { get; }

    public Guid ResourceId { get; }

    public long Version { get; }
}

public sealed record AuditRecord
{
    public AuditRecord(
        Guid auditId,
        TenantId tenantId,
        ActorId actorId,
        CommandId commandId,
        CorrelationId correlationId,
        ActionCode action,
        ResourceReference resource,
        DateTimeOffset occurredAtUtc,
        JsonElement? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action.Value);
        ArgumentNullException.ThrowIfNull(resource);
        UtcValue.Require(occurredAtUtc, nameof(occurredAtUtc));
        if (metadata.HasValue && metadata.Value.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Audit metadata must be an object.", nameof(metadata));

        AuditId = IdValue.Require(auditId, nameof(auditId));
        TenantId = tenantId;
        ActorId = actorId;
        CommandId = commandId;
        CorrelationId = correlationId;
        Action = action;
        Resource = resource;
        OccurredAtUtc = occurredAtUtc;
        Metadata = metadata?.Clone();
    }

    public Guid AuditId { get; init; }

    public TenantId TenantId { get; }

    public ActorId ActorId { get; }

    public CommandId CommandId { get; }

    public CorrelationId CorrelationId { get; }

    public ActionCode Action { get; init; }

    public ResourceReference Resource { get; }

    public DateTimeOffset OccurredAtUtc { get; init; }

    public JsonElement? Metadata { get; }
}

public sealed record OutboxMessage
{
    public OutboxMessage(
        Guid eventId,
        TenantId tenantId,
        CommandId causationId,
        CorrelationId correlationId,
        EventTypeCode eventType,
        ResourceReference aggregate,
        JsonElement payload,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType.Value);
        ArgumentNullException.ThrowIfNull(aggregate);
        UtcValue.Require(occurredAtUtc, nameof(occurredAtUtc));

        if (payload.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("An outbox payload is required.", nameof(payload));
        }

        EventId = IdValue.Require(eventId, nameof(eventId));
        TenantId = tenantId;
        CausationId = causationId;
        CorrelationId = correlationId;
        EventType = eventType;
        Aggregate = aggregate;
        Payload = payload.Clone();
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid EventId { get; }

    public TenantId TenantId { get; }

    public CommandId CausationId { get; }

    public CorrelationId CorrelationId { get; }

    public EventTypeCode EventType { get; }

    public ResourceReference Aggregate { get; }

    public JsonElement Payload { get; }

    public DateTimeOffset OccurredAtUtc { get; }
}

public sealed record CommandOutcome
{
    public CommandOutcome(
        JsonElement data,
        long aggregateVersion,
        AuditRecord audit,
        OutboxMessage outbox,
        IEnumerable<AuditRecord>? additionalAudits = null,
        IEnumerable<OutboxMessage>? additionalOutbox = null,
        JsonElement? persistedData = null)
    {
        if (data.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("Canonical command data is required.", nameof(data));
        }
        if (persistedData?.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("Persisted command data is invalid.", nameof(persistedData));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(aggregateVersion, 1);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(outbox);

        var audits = additionalAudits?.ToArray() ?? [];
        var messages = additionalOutbox?.ToArray() ?? [];
        ValidateAdditionalConsequences(audit, outbox, audits, messages);

        Data = data.Clone();
        PersistedData = (persistedData ?? data).Clone();
        AggregateVersion = aggregateVersion;
        Audit = audit;
        Outbox = outbox;
        AdditionalAudits = audits;
        AdditionalOutbox = messages;
    }

    public JsonElement Data { get; }

    public JsonElement PersistedData { get; }

    public long AggregateVersion { get; }

    public AuditRecord Audit { get; }

    public OutboxMessage Outbox { get; }

    public IReadOnlyList<AuditRecord> AdditionalAudits { get; }

    public IReadOnlyList<OutboxMessage> AdditionalOutbox { get; }

    public CommandOutcome WithAdditional(AuditRecord audit, OutboxMessage outbox) => new(
        Data,
        AggregateVersion,
        Audit,
        Outbox,
        AdditionalAudits.Append(audit),
        AdditionalOutbox.Append(outbox),
        PersistedData);

    private static void ValidateAdditionalConsequences(
        AuditRecord primaryAudit,
        OutboxMessage primaryOutbox,
        AuditRecord[] audits,
        OutboxMessage[] messages)
    {
        if (audits.Any(item =>
                item.TenantId != primaryAudit.TenantId ||
                item.ActorId != primaryAudit.ActorId ||
                item.CommandId != primaryAudit.CommandId ||
                item.CorrelationId != primaryAudit.CorrelationId) ||
            messages.Any(item =>
                item.TenantId != primaryOutbox.TenantId ||
                item.CausationId != primaryOutbox.CausationId ||
                item.CorrelationId != primaryOutbox.CorrelationId))
        {
            throw new ArgumentException(
                "Additional consequences must share the command tenant, actor and correlation.");
        }

        if (audits.Prepend(primaryAudit).Select(item => item.AuditId).Distinct().Count()
                != audits.Length + 1 ||
            messages.Prepend(primaryOutbox).Select(item => item.EventId).Distinct().Count()
                != messages.Length + 1)
        {
            throw new ArgumentException("Consequence identifiers must be unique.");
        }
    }
}

public enum CommandDisposition
{
    Applied = 1,
    Replayed = 2,
}

public sealed record CommandReceipt(
    CommandDisposition Disposition,
    CommandOutcome Outcome,
    AuditRecord ReceiptAudit);

internal static class UtcValue
{
    public static void Require(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The timestamp must be UTC.", parameterName);
        }
    }
}
