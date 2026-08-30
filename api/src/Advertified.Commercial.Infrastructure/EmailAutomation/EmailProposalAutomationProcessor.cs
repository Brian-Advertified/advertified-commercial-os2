using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Proposal;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailProposalAutomationProcessor(
    EmailAutomationRecordStore store,
    ISuppliedBriefUnderstandingService briefUnderstanding,
    IBriefCommands briefCommands,
    IBriefReader briefReader,
    IPlanningCommands planningCommands,
    IPlanningReader planningReader,
    IProposalCommands proposalCommands,
    IProposalReader proposalReader,
    IEmailProviderResolver emailProviders,
    IAutomationCommandEnvelopeFactory envelopes,
    IStpReadinessEvaluator stpReadiness,
    EmailAutomationInventorySelector inventorySelector,
    EmailAutomationPolicy policy,
    ProposalPolicy proposalPolicy,
    IOptions<EmailAutomationOptions> options,
    TimeProvider timeProvider) : IEmailProposalAutomationProcessor
{
    public async Task<EmailAutomationRunView> ProcessAsync(
        TenantId tenantId,
        ActorId actorId,
        Guid inboundEmailId,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var context = await LoadContextAsync(
            tenantId, actorId, inboundEmailId, cancellationToken);
        if (context.RunStatus == MasterDataCodes.EmailAutomationStatuses.Sent)
        {
            return await LoadRunViewAsync(
                tenantId, actorId, inboundEmailId, cancellationToken);
        }
        if (context.RunStatus is MasterDataCodes.EmailAutomationStatuses.ReviewRequired or
            MasterDataCodes.EmailAutomationStatuses.Failed)
        {
            throw new EmailAutomationNotRetryableException();
        }
        var owner = new ActorId(context.OwnerUserId);
        try
        {
            ValidateEntry(context);
            var run = await store.UpdateRunAsync(
                tenantId,
                owner,
                inboundEmailId,
                current => current with
                {
                    Status = MasterDataCodes.EmailAutomationStatuses.Processing,
                    FailureCode = null,
                    FailureMessage = null,
                    UpdatedAtUtc = timeProvider.GetUtcNow(),
                },
                new EmailAutomationTransition(
                    new CommandId(Guid.NewGuid()),
                    correlationId,
                    MasterDataReferences.CommercialActions.EmailAutomationStarted,
                    MasterDataReferences.CommercialEventTypes.EmailProposalAutomationStarted),
                cancellationToken);
            var understandingResult = await EnsureUnderstandingAsync(
                context, run, owner, cancellationToken);
            run = understandingResult.Run;
            var understanding = understandingResult.Understanding;
            run = await EnsureBriefAndStpAsync(
                context, run, understanding, owner, correlationId, cancellationToken);
            run = await EnsurePlanningAsync(
                context, run, understanding, owner, correlationId, cancellationToken);
            run = await EnsureProposalAndDeliveryAsync(
                context, run, understanding, owner, correlationId, cancellationToken);
            return EmailAutomationRecordStore.ToView(run);
        }
        catch (EmailAutomationReviewRequiredException exception)
        {
            var run = await SetFailureStateAsync(
                tenantId,
                owner,
                inboundEmailId,
                MasterDataCodes.EmailAutomationStatuses.ReviewRequired,
                exception.FailureCode,
                exception.Message,
                correlationId,
                MasterDataReferences.CommercialActions.EmailAutomationReviewRequired,
                MasterDataReferences.CommercialEventTypes.EmailProposalAutomationReviewRequired,
                cancellationToken);
            return EmailAutomationRecordStore.ToView(run);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            var run = await SetFailureStateAsync(
                tenantId,
                owner,
                inboundEmailId,
                MasterDataCodes.EmailAutomationStatuses.Failed,
                MasterDataCodes.AutomationFailureReasons.DeliveryFailed,
                "The proposal automation stopped before completion. Retry after checking the service.",
                correlationId,
                MasterDataReferences.CommercialActions.EmailAutomationFailed,
                MasterDataReferences.CommercialEventTypes.EmailProposalAutomationFailed,
                cancellationToken);
            return EmailAutomationRecordStore.ToView(run);
        }
    }

    private void ValidateEntry(EmailAutomationContextRow context)
    {
        if (!context.MailboxEnabled || !context.AutoSendEnabled ||
            !policy.AllowAutomaticExternalSend)
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.InvalidMailbox,
                "Automatic proposal delivery is not enabled for this mailbox.");
        }
        if (!policy.AllowAttachments && context.AttachmentCount > 0)
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.AttachmentReviewRequired,
                "This email includes an attachment that must be reviewed before a proposal can be prepared.");
        }
        var domains = EmailAutomationRecordStore.Read<string[]>(
            context.AllowedSenderDomainsJson);
        if (domains.Length == 0)
        {
            return;
        }
        var senderDomain = context.SenderEmail[(context.SenderEmail.LastIndexOf('@') + 1)..];
        if (!domains.Any(domain => senderDomain == domain ||
                senderDomain.EndsWith(string.Concat(".", domain), StringComparison.Ordinal)))
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.InvalidRecipient,
                "The sender is not permitted to use this proposal mailbox.");
        }
    }

    private async Task<EmailAutomationContextRow> LoadContextAsync(
        TenantId tenantId,
        ActorId actorId,
        Guid inboundEmailId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var context = await store.FindContextAsync(
            tenantId, inboundEmailId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Email automation access denied.");
        await transaction.CommitAsync(cancellationToken);
        return context;
    }

    private async Task<EmailAutomationRunView> LoadRunViewAsync(
        TenantId tenantId,
        ActorId actorId,
        Guid inboundEmailId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var run = await store.FindRunAsync(tenantId, inboundEmailId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Email automation access denied.");
        await transaction.CommitAsync(cancellationToken);
        return EmailAutomationRecordStore.ToView(run);
    }

    private Task<EmailAutomationRunRow> SetFailureStateAsync(
        TenantId tenantId,
        ActorId actorId,
        Guid inboundEmailId,
        string status,
        string failureCode,
        string message,
        CorrelationId correlationId,
        ActionCode action,
        EventTypeCode eventType,
        CancellationToken cancellationToken) =>
        store.UpdateRunAsync(
            tenantId,
            actorId,
            inboundEmailId,
            current => current with
            {
                Status = status,
                FailureCode = failureCode,
                FailureMessage = message,
                UpdatedAtUtc = timeProvider.GetUtcNow(),
            },
            new EmailAutomationTransition(
                new CommandId(Guid.NewGuid()),
                correlationId,
                action,
                eventType),
            cancellationToken);
}
