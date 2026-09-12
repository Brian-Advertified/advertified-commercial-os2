using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed partial class ProposalCommands
{
    private Task<ProposalCampaignContextView?> BuildCampaignContextAsync(
        TenantId tenantId,
        ProposalVersionView proposal,
        CancellationToken cancellationToken) =>
        ProposalCampaignContextBuilder.BuildAsync(
            planningStore, tenantId, proposal, cancellationToken);
}
