using Advertified.Commercial.Application.Campaign;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Campaign;

public sealed partial class CampaignCommands
{
    private async Task<CommandOutcome> StartOutcomeAsync(
        Guid campaignId,
        CommandEnvelope<StartCampaignCommand> envelope,
        CancellationToken cancellationToken)
    {
        var campaign = await store.FindAsync(campaignId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Campaign access denied.");
        if (campaign.Status != MasterDataCodes.LifecycleStatuses.Ready)
            throw new InvalidLifecycleTransitionException();
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (today < campaign.StartDate || today > campaign.EndDate)
            throw new CampaignDeliveryBlockedException();
        await store.StartAsync(
            campaign, envelope, RequiredReason(envelope.Command.Reason), now, cancellationToken);
        return await DeliveryOutcomeAsync(
            campaignId, envelope, MasterDataReferences.CommercialActions.CampaignStarted,
            MasterDataReferences.CommercialEventTypes.CampaignStarted, now, cancellationToken);
    }

    private async Task<CommandOutcome> CompleteOutcomeAsync(
        Guid campaignId,
        CommandEnvelope<CompleteCampaignCommand> envelope,
        CancellationToken cancellationToken)
    {
        var campaign = await store.FindAsync(campaignId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Campaign access denied.");
        if (campaign.Status != MasterDataCodes.LifecycleStatuses.Live)
            throw new InvalidLifecycleTransitionException();
        var now = timeProvider.GetUtcNow();
        if (DateOnly.FromDateTime(now.UtcDateTime) <= campaign.EndDate)
            throw new CampaignDeliveryBlockedException();
        await store.CompleteAsync(
            campaign, envelope, RequiredReason(envelope.Command.CompletionReason),
            RequiredReason(envelope.Command.ProofRequestReason), now, cancellationToken);
        return await DeliveryOutcomeAsync(
            campaignId, envelope, MasterDataReferences.CommercialActions.CampaignCompleted,
            MasterDataReferences.CommercialEventTypes.CampaignCompleted, now, cancellationToken);
    }

    private async Task<CommandOutcome> DeliveryOutcomeAsync<TCommand>(
        Guid campaignId,
        CommandEnvelope<TCommand> envelope,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var updated = await store.FindAsync(campaignId, false, cancellationToken)
            ?? throw new InvalidOperationException("The campaign was not persisted.");
        return CommandOutcomeFactory.Create(
            envelope, updated.ToView(), updated.Id, updated.Version,
            MasterDataReferences.CommercialResourceTypes.Campaign, action, eventType, now);
    }
}
