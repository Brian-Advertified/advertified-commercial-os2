using System.Text.Json;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Campaign;

internal static class CampaignConsequences
{
    internal static (AuditRecord Audit, OutboxMessage Outbox) Planned<TCommand>(
        CommandEnvelope<TCommand> envelope,
        CampaignRow campaign,
        DateTimeOffset now)
        where TCommand : notnull => Create(
            envelope, campaign, MasterDataReferences.CommercialActions.CampaignPlanned,
            MasterDataReferences.CommercialEventTypes.CampaignPlanned, now);

    private static (AuditRecord, OutboxMessage) Create<TCommand>(
        CommandEnvelope<TCommand> envelope,
        CampaignRow campaign,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset now)
        where TCommand : notnull
    {
        var resource = new ResourceReference(
            MasterDataReferences.CommercialResourceTypes.Campaign,
            campaign.Id,
            campaign.Version);
        var payload = JsonSerializer.SerializeToElement(campaign.ToView());
        return (
            new AuditRecord(
                Guid.NewGuid(), envelope.TenantId, envelope.ActorId,
                envelope.CommandId, envelope.CorrelationId, action, resource, now),
            new OutboxMessage(
                Guid.NewGuid(), envelope.TenantId, envelope.CommandId,
                envelope.CorrelationId, eventType, resource, payload, now));
    }
}
