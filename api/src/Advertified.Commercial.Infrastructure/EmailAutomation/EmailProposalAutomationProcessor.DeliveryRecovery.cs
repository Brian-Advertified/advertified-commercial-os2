using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailProposalAutomationProcessor
{
    private async Task<EmailAutomationRunRow> ResumeDeliveryAsync(
        EmailAutomationContextRow context,
        EmailAutomationRunRow run,
        ActorId owner,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        if (run.DeliveryAcceptedAtUtc.HasValue && run.DeliveryProviderId is not null)
        {
            return await FinalizeDeliveryAsync(
                context, run, owner, correlationId, cancellationToken);
        }
        var providerCode = run.DeliveryProviderCode
            ?? throw new InvalidOperationException("The delivery provider was not retained.");
        var idempotencyKey = run.DeliveryIdempotencyKey
            ?? throw new InvalidOperationException("The delivery request key was not retained.");
        EmailDeliveryReceipt receipt;
        try
        {
            receipt = await durableDelivery.RecoverAsync(new TenantId(context.TenantId), owner,
                run.ProposalVersionId ?? throw new InvalidOperationException("The delivery proposal was not retained."),
                providerCode, idempotencyKey, cancellationToken);
        }
        catch (EmailDeliveryAcceptanceUnknownException) { throw DeliveryAmbiguous(); }
        return await AcceptAndFinalizeAsync(
            context, run, owner, receipt, correlationId, cancellationToken);
    }

    private async Task<EmailAutomationRunRow> AcceptAndFinalizeAsync(
        EmailAutomationContextRow context,
        EmailAutomationRunRow run,
        ActorId owner,
        EmailDeliveryReceipt receipt,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        run = await store.RecordDeliveryAcceptanceAsync(
            new TenantId(context.TenantId), owner, context.InboundEmailId,
            receipt.ProviderMessageId, receipt.AcceptedAtUtc,
            timeProvider.GetUtcNow(), correlationId, cancellationToken);
        return await FinalizeDeliveryAsync(
            context, run, owner, correlationId, cancellationToken);
    }

    private async Task<EmailAutomationRunRow> FinalizeDeliveryAsync(
        EmailAutomationContextRow context,
        EmailAutomationRunRow run,
        ActorId owner,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var tenantId = new TenantId(context.TenantId);
        var proposalId = run.ProposalVersionId
            ?? throw new InvalidOperationException("The accepted delivery has no proposal.");
        var providerId = run.DeliveryProviderId
            ?? throw new InvalidOperationException("The accepted delivery has no provider receipt.");
        var proposal = await proposalReader.GetAsync(
            owner, tenantId, proposalId, cancellationToken);
        if (proposal.Status == MasterDataCodes.LifecycleStatuses.Approved)
        {
            proposal = (await proposalCommands.RecordAutomatedDeliveryAsync(
                proposal.Id,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.ProposalSend,
                    proposal.Version,
                    new RecordAutomatedProposalDeliveryCommand(
                        run.Id, context.ReplyToEmail, providerId),
                    correlationId),
                cancellationToken)).Data;
        }
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.Sent)
        {
            throw ReviewRequired(
                MasterDataCodes.AutomationFailureReasons.DeliveryFailed,
                "The proposal delivery could not be recorded.");
        }
        return await RecordSentRunAsync(
            context, owner, correlationId, cancellationToken);
    }

    private Task<EmailAutomationRunRow> RecordSentRunAsync(
        EmailAutomationContextRow context,
        ActorId owner,
        CorrelationId correlationId,
        CancellationToken cancellationToken) =>
        store.UpdateRunAsync(
            new TenantId(context.TenantId),
            owner,
            context.InboundEmailId,
            current => current.Status == MasterDataCodes.EmailAutomationStatuses.Sent
                ? current
                : current with
                {
                    Status = MasterDataCodes.EmailAutomationStatuses.Sent,
                    Checkpoint = MasterDataCodes.EmailAutomationCheckpoints.Sent,
                    FailureCode = null,
                    FailureMessage = null,
                    UpdatedAtUtc = timeProvider.GetUtcNow(),
                },
            new EmailAutomationTransition(
                new CommandId(Guid.NewGuid()),
                correlationId,
                MasterDataReferences.CommercialActions.EmailAutomationSent,
                MasterDataReferences.CommercialEventTypes.EmailProposalAutomationSent),
            cancellationToken);

    private static EmailAutomationReviewRequiredException DeliveryAmbiguous() =>
        ReviewRequired(
            MasterDataCodes.AutomationFailureReasons.DeliveryAmbiguous,
            AmbiguousDeliveryMessage);
}
