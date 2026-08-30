using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Campaign;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Creative;

public sealed partial class CreativeCommands
{
    private async Task<CommandOutcome> RequestOutcomeAsync(
        Guid campaignId,
        CommandEnvelope<RequestCampaignCreativeCommand> envelope,
        CancellationToken cancellationToken)
    {
        var campaign = await campaignStore.FindAsync(campaignId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Campaign access denied.");
        if (campaign.Status != MasterDataCodes.LifecycleStatuses.Booked)
            throw new InvalidLifecycleTransitionException();
        var sources = await store.ListConfirmedBookingsAsync(campaignId, cancellationToken);
        var requirements = CreativeInputPolicy.PrepareRequirements(
            envelope.Command.Requirements, sources);
        var reason = CreativeInputPolicy.Reason(envelope.Command.Reason);
        var now = timeProvider.GetUtcNow();
        await store.InsertRequirementsAsync(
            campaignId, envelope, requirements, now, cancellationToken);
        await campaignStore.RequestCreativeAsync(
            campaign, envelope, reason, now, cancellationToken);
        var updated = await campaignStore.FindAsync(campaignId, false, cancellationToken)
            ?? throw new InvalidOperationException("The campaign was not persisted.");
        return CampaignOutcome(
            envelope, updated, MasterDataReferences.CommercialActions.CampaignCreativeRequested,
            MasterDataReferences.CommercialEventTypes.CampaignCreativeRequested, now);
    }

    private async Task<CommandOutcome> ApproveCampaignOutcomeAsync(
        Guid campaignId,
        CommandEnvelope<ApproveCampaignCreativeCommand> envelope,
        CancellationToken cancellationToken)
    {
        var campaign = await campaignStore.FindAsync(campaignId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Campaign access denied.");
        if (campaign.Status != MasterDataCodes.LifecycleStatuses.CreativePending)
            throw new InvalidLifecycleTransitionException();
        var workspace = CreativeRecordStore.ToWorkspace(
            await store.ListWorkspaceRowsAsync(campaignId, cancellationToken));
        if (!workspace.ReadyForApproval) throw new CreativeReadinessBlockedException();
        var reason = CreativeInputPolicy.Reason(envelope.Command.Reason);
        var now = timeProvider.GetUtcNow();
        await campaignStore.ApproveCreativeAsync(
            campaign, envelope, reason, now, cancellationToken);
        var updated = await campaignStore.FindAsync(campaignId, false, cancellationToken)
            ?? throw new InvalidOperationException("The campaign was not persisted.");
        return CampaignOutcome(
            envelope, updated, MasterDataReferences.CommercialActions.CampaignCreativeApproved,
            MasterDataReferences.CommercialEventTypes.CampaignCreativeApproved, now);
    }

    private static CommandOutcome CampaignOutcome<TCommand>(
        CommandEnvelope<TCommand> envelope,
        CampaignRow campaign,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset now)
        where TCommand : notnull => CommandOutcomeFactory.Create(
            envelope, campaign.ToView(), campaign.Id, campaign.Version,
            MasterDataReferences.CommercialResourceTypes.Campaign, action, eventType, now);
}
