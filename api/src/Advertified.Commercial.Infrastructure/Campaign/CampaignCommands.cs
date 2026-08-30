using Advertified.Commercial.Application.Campaign;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Campaign;

public sealed partial class CampaignCommands(
    CampaignRecordStore store,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider) : ICampaignCommands
{
    public async Task<CommandResult<CampaignView>> ConfirmBookingsAsync(
        Guid campaignId,
        CommandEnvelope<ConfirmCampaignBookingsCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.CampaignConfirmBookings,
            token => ConfirmOutcomeAsync(campaignId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<CampaignView>(receipt);
    }

    public async Task<CommandResult<CampaignView>> StartAsync(
        Guid campaignId,
        CommandEnvelope<StartCampaignCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.CampaignStart,
            token => StartOutcomeAsync(campaignId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<CampaignView>(receipt);
    }

    public async Task<CommandResult<CampaignView>> CompleteAsync(
        Guid campaignId,
        CommandEnvelope<CompleteCampaignCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.CampaignComplete,
            token => CompleteOutcomeAsync(campaignId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<CampaignView>(receipt);
    }

    private async Task<CommandOutcome> ConfirmOutcomeAsync(
        Guid campaignId,
        CommandEnvelope<ConfirmCampaignBookingsCommand> envelope,
        CancellationToken cancellationToken)
    {
        var campaign = await store.FindAsync(campaignId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Campaign access denied.");
        if (campaign.Status != MasterDataCodes.LifecycleStatuses.Planned)
            throw new InvalidLifecycleTransitionException();
        var reason = RequiredReason(envelope.Command.Reason);
        var now = timeProvider.GetUtcNow();
        await store.ConfirmBookingsAsync(
            campaign, envelope, reason, now, cancellationToken);
        var updated = await store.FindAsync(campaignId, false, cancellationToken)
            ?? throw new InvalidOperationException("The campaign was not persisted.");
        return CommandOutcomeFactory.Create(
            envelope, updated.ToView(), updated.Id, updated.Version,
            MasterDataReferences.CommercialResourceTypes.Campaign,
            MasterDataReferences.CommercialActions.CampaignBookingsConfirmed,
            MasterDataReferences.CommercialEventTypes.CampaignBookingsConfirmed, now);
    }

    private static string RequiredReason(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A booking-readiness reason is required.");
        var result = value.Trim();
        return result.Length <= 1_000
            ? result
            : throw new ArgumentException("The booking-readiness reason is too long.");
    }
}
