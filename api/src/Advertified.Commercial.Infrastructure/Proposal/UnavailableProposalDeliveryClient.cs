using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Proposal;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed class UnavailableProposalDeliveryClient : IProposalDeliveryClient
{
    public Task<ProposalDeliveryReceipt> DeliverAsync(
        ProposalDeliveryRequest request, CancellationToken cancellationToken) =>
        throw new EmailProviderUnavailableException();
}
