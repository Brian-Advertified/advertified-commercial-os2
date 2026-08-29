using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Infrastructure.Persistence.Records;

public sealed class AuditEventRow
{
    private AuditEventRow()
    {
    }

    public AuditEventRow(AuditRecord record, string metadataJson)
    {
        Id = record.AuditId;
        TenantId = record.TenantId;
        ActorId = record.ActorId;
        CommandId = record.CommandId;
        CorrelationId = record.CorrelationId;
        Action = record.Action;
        ResourceType = record.Resource.ResourceType;
        ResourceId = record.Resource.ResourceId;
        ResourceVersion = record.Resource.Version;
        OccurredAtUtc = record.OccurredAtUtc;
        MetadataJson = metadataJson;
    }

    public Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public ActorId ActorId { get; private set; }

    public CommandId CommandId { get; private set; }

    public CorrelationId CorrelationId { get; private set; }

    public ActionCode Action { get; private set; }

    public ResourceTypeCode ResourceType { get; private set; }

    public Guid ResourceId { get; private set; }

    public long ResourceVersion { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string MetadataJson { get; private set; } = "{}";
}
