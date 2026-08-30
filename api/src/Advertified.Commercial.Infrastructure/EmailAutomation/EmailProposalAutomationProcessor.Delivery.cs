using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailProposalAutomationProcessor
{
    private async Task<EmailAutomationRunRow> EnsureProposalAndDeliveryAsync(
        EmailAutomationContextRow context,
        EmailAutomationRunRow run,
        SuppliedBriefUnderstandingView understanding,
        ActorId owner,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var tenantId = new TenantId(context.TenantId);
        var briefId = run.BriefId
            ?? throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.IncompleteBrief,
                "The campaign Brief is not available for proposal creation.");
        var planId = run.MediaPlanVersionId
            ?? throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.PlanUnready,
                "The approved media plan is not available for proposal creation.");
        var expiry = context.ReceivedAtUtc.AddDays(proposalPolicy.DefaultValidityDays);
        if (expiry <= timeProvider.GetUtcNow())
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.ProposalUnready,
                "The inbound request is too old for automatic proposal delivery. Start a new campaign request.");
        }

        ProposalVersionView proposal;
        if (!run.ProposalVersionId.HasValue)
        {
            var generated = await proposalCommands.GenerateAsync(
                briefId,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.ProposalGenerate,
                    0,
                    new GenerateProposalCommand(
                        policy.ProposalTitle,
                        [new ProposalOptionInput(
                            planId,
                            policy.ProposalOptionLabel,
                            understanding.Draft.Objective)],
                        policy.ProposalTerms,
                        expiry),
                    correlationId),
                cancellationToken);
            proposal = generated.Data;
            run = await store.UpdateRunAsync(
                tenantId,
                owner,
                context.InboundEmailId,
                current => current with
                {
                    ProposalVersionId = proposal.Id,
                    UpdatedAtUtc = timeProvider.GetUtcNow(),
                },
                cancellationToken);
        }
        else
        {
            proposal = await proposalReader.GetAsync(
                owner, tenantId, run.ProposalVersionId.Value, cancellationToken);
        }

        if (proposal.Options.Count != policy.MaximumProposalOptions ||
            proposal.Options.Any(option => option.PlanVersionId != planId ||
                option.Channels.Any(channel =>
                    !policy.AllowedChannels.Contains(channel, StringComparer.Ordinal))))
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.ProposalUnready,
                "The proposal does not reference the exact approved OOH plan.");
        }
        if (proposal.Status == MasterDataCodes.LifecycleStatuses.Draft)
        {
            proposal = (await proposalCommands.ApproveAsync(
                proposal.Id,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.ProposalApprove,
                    proposal.Version,
                    new ApproveProposalCommand(
                        "The proposal is bound to the exact approved OOH plan and the mailbox permits automatic delivery."),
                    correlationId),
                cancellationToken)).Data;
        }
        if (proposal.Status is not (MasterDataCodes.LifecycleStatuses.Approved or
            MasterDataCodes.LifecycleStatuses.Sent))
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.ProposalUnready,
                "The proposal is not approved for automatic delivery.");
        }
        run = await store.UpdateRunAsync(
            tenantId,
            owner,
            context.InboundEmailId,
            current => current with
            {
                ProposalVersionId = proposal.Id,
                Checkpoint = MasterDataCodes.EmailAutomationCheckpoints.ProposalApproved,
                UpdatedAtUtc = timeProvider.GetUtcNow(),
            },
            cancellationToken);

        if (proposal.Document is null)
        {
            if (proposal.Status != MasterDataCodes.LifecycleStatuses.Approved)
            {
                throw new EmailAutomationReviewRequiredException(
                    MasterDataCodes.AutomationFailureReasons.ProposalUnready,
                    "The sent proposal has no retained document.");
            }
            proposal = (await proposalCommands.RenderAsync(
                proposal.Id,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.ProposalRender,
                    proposal.Version, new RenderProposalCommand(), correlationId),
                cancellationToken)).Data;
        }
        var document = proposal.Document
            ?? throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.ProposalUnready,
                "The approved proposal could not be rendered.");
        run = await store.UpdateRunAsync(
            tenantId,
            owner,
            context.InboundEmailId,
            current => current with
            {
                DocumentId = document.Id,
                Checkpoint = MasterDataCodes.EmailAutomationCheckpoints.DocumentRendered,
                DeliveryIdempotencyKey = current.DeliveryIdempotencyKey ??
                    BuildDeliveryIdempotencyKey(current.Id, context.ReplyToEmail),
                UpdatedAtUtc = timeProvider.GetUtcNow(),
            },
            cancellationToken);

        var content = await proposalReader.GetDocumentAsync(
            owner, tenantId, document.Id, cancellationToken);
        var provider = emailProviders.Resolve(context.Provider);
        var sender = string.IsNullOrWhiteSpace(options.Value.SenderAddress)
            ? context.MailboxAddress
            : options.Value.SenderAddress.Trim();
        var delivery = await provider.SendAsync(
            new ProposalEmailDelivery(
                context.ReplyToEmail,
                sender,
                string.Concat(policy.EmailSubjectPrefix, " ", proposal.Title),
                policy.EmailBody,
                content.FileName,
                content.MediaType,
                content.Content,
                context.ProviderMessageId,
                run.DeliveryIdempotencyKey!),
            cancellationToken);

        if (proposal.Status == MasterDataCodes.LifecycleStatuses.Approved)
        {
            proposal = (await proposalCommands.RecordAutomatedDeliveryAsync(
                proposal.Id,
                envelopes.Create(
                    tenantId, owner, run.Id, EmailAutomationStageNames.ProposalSend,
                    proposal.Version,
                    new RecordAutomatedProposalDeliveryCommand(
                        run.Id,
                        context.ReplyToEmail,
                        delivery.ProviderMessageId),
                    correlationId),
                cancellationToken)).Data;
        }
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.Sent)
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.DeliveryFailed,
                "The proposal delivery could not be recorded.");
        }

        return await store.UpdateRunAsync(
            tenantId,
            owner,
            context.InboundEmailId,
            current => current with
            {
                Status = MasterDataCodes.EmailAutomationStatuses.Sent,
                Checkpoint = MasterDataCodes.EmailAutomationCheckpoints.Sent,
                DeliveryProviderId = delivery.ProviderMessageId,
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
    }

    private static string BuildDeliveryIdempotencyKey(Guid runId, string recipient) =>
        string.Concat(
            "proposal-email-",
            OpportunityCommandSupport.Hash(
                string.Concat(runId.ToString("N"), ":", recipient.ToLowerInvariant())));
}
