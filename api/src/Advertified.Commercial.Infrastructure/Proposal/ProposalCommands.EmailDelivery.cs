using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Advertified.Commercial.Infrastructure.Opportunity;
using Advertified.Commercial.Infrastructure.Persistence;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed partial class ProposalCommands
{
    private async Task<EmailDeliveryReceipt> SendProposalAsync(
        Guid proposalVersionId, CommandEnvelope<ShareProposalCommand> envelope,
        CancellationToken cancellationToken)
    {
        var permission = await authorizer.AuthorizeAsync(envelope.ActorId, envelope.TenantId,
            MasterDataReferences.Permissions.ProposalShare, cancellationToken);
        if (!permission.IsAllowed) throw new UnauthorizedAccessException("Proposal sharing access denied.");
        var provider = emailOptions.Value.Mode switch
        {
            EmailAutomationOptions.DeterministicMode => MasterDataCodes.EmailProviders.Deterministic,
            EmailAutomationOptions.ResendMode => MasterDataCodes.EmailProviders.Resend,
            _ => throw new EmailProviderUnavailableException(),
        };
        var sender = emailOptions.Value.SenderAddress;
        if (string.IsNullOrWhiteSpace(sender)) throw new EmailProviderUnavailableException();
        ProposalDocumentRow document;
        ProposalEmailDelivery delivery;
        await using (var transaction = await store.BeginSessionAsync(envelope.ActorId, envelope.TenantId, cancellationToken))
        {
            var proposal = await LoadOwnedProposalAsync(proposalVersionId, envelope, cancellationToken);
            var recipient = await store.FindRecipientAsync(envelope.TenantId, envelope.Command.RecipientUserId, cancellationToken);
            if (recipient is null || recipient.Status != MasterDataCodes.LifecycleStatuses.Active ||
                recipient.Role is not (MasterDataCodes.Roles.AdvertiserAdmin or MasterDataCodes.Roles.AdvertiserApprover))
                throw new UnauthorizedAccessException("Client recipient is unavailable.");
            document = await store.FindDocumentAsync(envelope.TenantId, proposalVersionId, cancellationToken)
                ?? throw new ProposalDocumentRequiredException();
            var key = OpportunityCommandSupport.Hash(
                $"proposal-email-v1:{envelope.TenantId.Value:N}:{proposalVersionId:N}:{document.Id:N}:{recipient.UserId:N}");
            delivery = new ProposalEmailDelivery(recipient.Email, sender.Trim(), proposal.Title,
                proposal.ExecutiveSummary, document.FileName, document.MediaType, document.Content, null, key);
            await transaction.CommitAsync(cancellationToken);
        }
        return await emailDelivery.DeliverAsync(new ProposalEmailBinding(
                envelope.TenantId, envelope.ActorId, proposalVersionId, document.Id,
                envelope.ExpectedVersion, provider, CommandIntentIdentity.From(envelope)),
            delivery, true, cancellationToken);
    }
}
