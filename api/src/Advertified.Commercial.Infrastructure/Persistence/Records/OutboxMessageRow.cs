using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Infrastructure.Persistence.Records;

public sealed class OutboxMessageRow
{
    private OutboxMessageRow()
    {
    }

    public OutboxMessageRow(OutboxMessage message)
    {
        Id = message.EventId;
        TenantId = message.TenantId;
        CausationId = message.CausationId;
        CorrelationId = message.CorrelationId;
        EventType = message.EventType;
        AggregateType = message.Aggregate.ResourceType;
        AggregateId = message.Aggregate.ResourceId;
        AggregateVersion = message.Aggregate.Version;
        PayloadJson = message.Payload.GetRawText();
        OccurredAtUtc = message.OccurredAtUtc;
    }

    public Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public CommandId CausationId { get; private set; }

    public CorrelationId CorrelationId { get; private set; }

    public EventTypeCode EventType { get; private set; }

    public ResourceTypeCode AggregateType { get; private set; }

    public Guid AggregateId { get; private set; }

    public long AggregateVersion { get; private set; }

    public string PayloadJson { get; private set; } = "{}";

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public int Attempts { get; private set; }
}
