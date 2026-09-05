using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed class DurableProposalEmailDelivery(
    ProposalEmailIntentStore intents, IEmailProviderResolver providers, ITenantAuthorizer authorizer)
{
    internal async Task<EmailDeliveryReceipt> DeliverAsync(
        ProposalEmailBinding binding, ProposalEmailDelivery delivery,
        bool allowNewSend, CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(binding.ActorId, binding.TenantId,
            MasterDataReferences.Permissions.ProposalShare, cancellationToken);
        if (!decision.IsAllowed) throw new UnauthorizedAccessException("Proposal delivery access denied.");
        var provider = providers.Resolve(binding.ProviderCode);
        var intent = await intents.PrepareAsync(binding, delivery, cancellationToken);
        if (intent.Row.ProviderMessageId is { } messageId && intent.Row.AcceptedAtUtc is { } acceptedAt)
            return new EmailDeliveryReceipt(messageId, acceptedAt);
        var receipt = await SendOrReconcileAsync(provider, delivery,
            intent.IsNew && allowNewSend, cancellationToken);
        // Retain acceptance even if the HTTP caller disconnects after the send.
        using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await intents.RecordAcceptanceAsync(binding, receipt, completion.Token);
        return receipt;
    }

    internal static async Task<EmailDeliveryReceipt> SendOrReconcileAsync(
        IEmailProviderClient provider, ProposalEmailDelivery delivery, bool shouldSend,
        CancellationToken cancellationToken)
    {
        try
        {
            if (shouldSend) return await provider.SendAsync(delivery, cancellationToken);
            return await ReconcileAsync(provider, delivery.IdempotencyKey, cancellationToken);
        }
        catch (EmailDeliveryFailedException) { throw; }
        catch (EmailDeliveryAcceptanceUnknownException) { throw; }
        catch (Exception exception)
        {
            // Neither cancellation nor a transport exception proves non-acceptance.
            throw new EmailDeliveryAcceptanceUnknownException(exception);
        }
    }

    internal async Task<EmailDeliveryReceipt> RecoverAsync(
        TenantId tenantId, ActorId actorId, Guid proposalId, string providerCode,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(actorId, tenantId,
            MasterDataReferences.Permissions.ProposalShare, cancellationToken);
        if (!decision.IsAllowed) throw new UnauthorizedAccessException("Proposal delivery access denied.");
        return await intents.ReadAcceptanceAsync(tenantId, actorId, proposalId, providerCode,
                idempotencyKey, cancellationToken)
            ?? await ReconcileAsync(providers.Resolve(providerCode), idempotencyKey, cancellationToken);
    }

    internal static async Task<EmailDeliveryReceipt> ReconcileAsync(
        IEmailProviderClient provider, string idempotencyKey, CancellationToken cancellationToken)
    {
        var result = await provider.ReconcileDeliveryAsync(idempotencyKey, cancellationToken);
        return result.Outcome == EmailDeliveryReconciliationOutcome.Accepted && result.Receipt is not null
            ? result.Receipt : throw new EmailDeliveryAcceptanceUnknownException();
    }
}
