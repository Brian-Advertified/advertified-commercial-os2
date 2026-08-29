using Advertified.Commercial.Application.Proposal;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed class DeterministicProposalDeliveryClient(TimeProvider timeProvider)
    : IProposalDeliveryClient
{
    public Task<ProposalDeliveryReceipt> DeliverAsync(
        ProposalDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProposalTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RecipientEmail);
        if (request.RecipientUserId == Guid.Empty ||
            !request.RecipientEmail.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("The client recipient is invalid.");
        }
        return Task.FromResult(new ProposalDeliveryReceipt(
            timeProvider.GetUtcNow(), 0));
    }
}
