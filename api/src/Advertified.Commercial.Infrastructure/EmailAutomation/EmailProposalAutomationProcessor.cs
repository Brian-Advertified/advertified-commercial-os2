using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Proposal;
using Advertified.Commercial.Infrastructure.Planning;
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
    IEmailAutomationInventorySelector inventorySelector,
    EmailAutomationPolicy policy,
    ProposalPolicy proposalPolicy,
    PlanningPolicy planningPolicy,
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
        EnsureProcessable(context);
        try
        {
            return await ProcessRunAsync(
                tenantId, context, actorId, correlationId, cancellationToken);
        }
        catch (EmailAutomationReviewRequiredException exception)
        {
            return await RecordReviewRequiredAsync(
                tenantId, actorId, inboundEmailId, exception,
                correlationId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EmailDeliveryFailedException)
        {
            return await RecordConfirmedDeliveryFailureAsync(
                tenantId, actorId, inboundEmailId,
                correlationId, cancellationToken);
        }
        catch
        {
            return await RecordUnexpectedFailureAsync(
                tenantId, context, actorId, correlationId, cancellationToken);
        }
    }

    private async Task<EmailAutomationRunView> ProcessRunAsync(
        TenantId tenantId,
        EmailAutomationContextRow context,
        ActorId owner,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        if (!HasDeliveryIntent(context))
        {
            ValidateEntry(context);
        }
        else if (!context.DeliveryAcceptedAtUtc.HasValue)
        {
            ValidateProviderMode(context.Provider);
        }
        var run = await StartProcessingAsync(
            tenantId, context.InboundEmailId, owner, correlationId, cancellationToken);
        if (run.Status == MasterDataCodes.EmailAutomationStatuses.Sent)
        {
            return EmailAutomationRecordStore.ToView(run);
        }
        if (run.DeliveryRequestedAtUtc.HasValue)
        {
            run = await ResumeDeliveryAsync(
                context, run, owner, correlationId, cancellationToken);
            return EmailAutomationRecordStore.ToView(run);
        }
        var understandingResult = await EnsureUnderstandingAsync(
            context, run, owner, cancellationToken);
        run = await EnsureBriefAndStpAsync(
            context, understandingResult.Run, understandingResult.Understanding,
            owner, correlationId, cancellationToken);
        run = await EnsurePlanningAsync(
            context, run, understandingResult.Understanding,
            owner, correlationId, cancellationToken);
        run = await EnsureProposalAndDeliveryAsync(
            context, run, understandingResult.Understanding,
            owner, correlationId, cancellationToken);
        return EmailAutomationRecordStore.ToView(run);
    }

    private Task<EmailAutomationRunRow> StartProcessingAsync(
        TenantId tenantId,
        Guid inboundEmailId,
        ActorId owner,
        CorrelationId correlationId,
        CancellationToken cancellationToken) =>
        store.UpdateRunAsync(
            tenantId,
            owner,
            inboundEmailId,
            current => current.Status == MasterDataCodes.EmailAutomationStatuses.Sent ||
                current.Status == MasterDataCodes.EmailAutomationStatuses.Processing &&
                current.FailureCode is null && current.FailureMessage is null
                ? current
                : current with
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

    private static void EnsureProcessable(EmailAutomationContextRow context)
    {
        if ((context.RunStatus is MasterDataCodes.EmailAutomationStatuses.ReviewRequired or
                MasterDataCodes.EmailAutomationStatuses.Failed) &&
            !HasDeliveryIntent(context))
        {
            throw new EmailAutomationNotRetryableException();
        }
        if (context.RunStatus == MasterDataCodes.EmailAutomationStatuses.Failed &&
            context.FailureCode == MasterDataCodes.AutomationFailureReasons.DeliveryFailed &&
            HasDeliveryIntent(context))
        {
            throw new EmailAutomationNotRetryableException();
        }
    }

    private static bool HasDeliveryIntent(EmailAutomationContextRow context) =>
        context.DeliveryRequestedAtUtc.HasValue;

    private void ValidateEntry(EmailAutomationContextRow context)
    {
        ValidateProviderMode(context.Provider);
        if (InboundCampaignIntentDetector.ContainsMultipleExplicitBriefs(context.BodyText))
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.IncompleteBrief,
                "This email contains multiple campaign Briefs that must be separated before planning.");
        }
        if (!context.MailboxEnabled || !context.AutoSendEnabled ||
            !policy.AllowAutomaticExternalSend)
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.InvalidMailbox,
                "Automatic proposal delivery is not enabled for this mailbox.");
        }
        if (!EmailContentNormalizer.IsAutomaticReplyVerified(context.RawMetadataJson))
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.InvalidRecipient,
                "The sender or reply address is not verified for automatic delivery.");
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

    private void ValidateProviderMode(string providerCode)
    {
        if (!options.Value.IsProviderEnabled(providerCode))
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.InvalidMailbox,
                "Automatic proposal delivery is disabled for this mailbox provider.");
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

}
