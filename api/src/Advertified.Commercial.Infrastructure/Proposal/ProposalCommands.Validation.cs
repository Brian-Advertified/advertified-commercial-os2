using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Advertified.Commercial.Infrastructure.Planning;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed partial class ProposalCommands
{

    private static void EnsureProposalInventoryCurrent(ProposalRow proposal)
    {
        if (proposal.InventoryReviewStatus !=
            MasterDataCodes.ProposalInventoryReviewStatuses.Current)
        {
            throw new ProposalInventoryReviewRequiredException();
        }
    }

    private static CommandOutcome ProposalOutcome<TCommand>(
        CommandEnvelope<TCommand> envelope,
        ProposalVersionView view,
        Guid resourceId,
        long version,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset now)
        where TCommand : notnull => OpportunityCommandSupport.Outcome(
            envelope, view, resourceId, version,
            MasterDataReferences.CommercialResourceTypes.ProposalVersion,
            action, eventType, now);
}
