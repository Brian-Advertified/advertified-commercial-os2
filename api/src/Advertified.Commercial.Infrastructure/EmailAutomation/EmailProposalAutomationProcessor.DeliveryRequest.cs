using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailProposalAutomationProcessor
{
    private async Task<EmailAutomationRunRow> BeginDeliveryAsync(
        EmailAutomationContextRow context,
        EmailAutomationRunRow run,
        ProposalVersionView proposal,
        ProposalDocumentView document,
        ActorId owner,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var tenantId = new TenantId(context.TenantId);
        var content = await proposalReader.GetDocumentAsync(
            owner, tenantId, document.Id, cancellationToken);
        var providerCode = run.DeliveryProviderCode ?? context.Provider;
        var provider = emailProviders.Resolve(providerCode);
        var idempotencyKey = run.DeliveryIdempotencyKey ??
            BuildDeliveryIdempotencyKey(run.Id, context.ReplyToEmail);
        var delivery = CreateDelivery(context, proposal, content, idempotencyKey);
        var intent = await store.BeginDeliveryAsync(
            tenantId, owner, context.InboundEmailId, providerCode, idempotencyKey,
            timeProvider.GetUtcNow(), correlationId, cancellationToken);
        var receipt = await ResolveDeliveryReceiptAsync(
            intent, provider, delivery, idempotencyKey, cancellationToken);
        try
        {
            return await AcceptAndFinalizeAsync(
                context, intent.Run, owner, receipt, correlationId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EmailAutomationReviewRequiredException)
        {
            throw;
        }
        catch
        {
            throw DeliveryAmbiguous();
        }
    }

    private static async Task<EmailDeliveryReceipt> ResolveDeliveryReceiptAsync(
        DeliveryIntentResult intent,
        IEmailProviderClient provider,
        ProposalEmailDelivery delivery,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!intent.ShouldSend)
        {
            return await ReconcileAsync(provider, idempotencyKey, cancellationToken);
        }
        try
        {
            return await provider.SendAsync(delivery, cancellationToken);
        }
        catch (EmailDeliveryAcceptanceUnknownException)
        {
            throw DeliveryAmbiguous();
        }
        catch (EmailDeliveryFailedException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            throw DeliveryAmbiguous();
        }
    }

    private ProposalEmailDelivery CreateDelivery(
        EmailAutomationContextRow context,
        ProposalVersionView proposal,
        ProposalDocumentContent content,
        string idempotencyKey)
    {
        var sender = string.IsNullOrWhiteSpace(options.Value.SenderAddress)
            ? context.MailboxAddress
            : options.Value.SenderAddress.Trim();
        return new ProposalEmailDelivery(
            context.ReplyToEmail,
            sender,
            string.Concat(policy.EmailSubjectPrefix, " ", proposal.Title),
            policy.EmailBody,
            content.FileName,
            content.MediaType,
            content.Content,
            context.ProviderMessageId,
            idempotencyKey);
    }
}
