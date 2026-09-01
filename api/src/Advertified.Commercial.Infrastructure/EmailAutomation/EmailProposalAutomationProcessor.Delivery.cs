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
        if (run.DeliveryRequestedAtUtc.HasValue)
        {
            return await ResumeDeliveryAsync(
                context, run, owner, correlationId, cancellationToken);
        }

        var tenantId = new TenantId(context.TenantId);
        var briefId = RequireBriefId(run);
        var planId = RequirePlanId(run);
        var expiry = context.ReceivedAtUtc.AddDays(proposalPolicy.DefaultValidityDays);
        if (expiry <= timeProvider.GetUtcNow())
        {
            throw ReviewRequired(
                MasterDataCodes.AutomationFailureReasons.ProposalUnready,
                "The inbound request is too old for automatic proposal delivery. Start a new campaign request.");
        }

        var proposal = await GetOrCreateProposalAsync(
            tenantId, owner, run, briefId, planId, understanding, expiry,
            correlationId, cancellationToken);
        proposal = await EnsureApprovedProposalAsync(
            tenantId, owner, run, planId, proposal, correlationId, cancellationToken);
        run = await RecordProposalCheckpointAsync(
            context, proposal.Id, owner, cancellationToken);
        var document = await GetOrRenderDocumentAsync(
            tenantId, owner, run, proposal, correlationId, cancellationToken);
        run = await RecordDocumentCheckpointAsync(
            context, document.Id, owner, cancellationToken);
        return await BeginDeliveryAsync(
            context, run, proposal, document, owner, correlationId, cancellationToken);
    }

    private async Task<ProposalVersionView> GetOrCreateProposalAsync(
        TenantId tenantId,
        ActorId owner,
        EmailAutomationRunRow run,
        Guid briefId,
        Guid planId,
        SuppliedBriefUnderstandingView understanding,
        DateTimeOffset expiry,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        if (run.ProposalVersionId.HasValue)
        {
            return await proposalReader.GetAsync(
                owner, tenantId, run.ProposalVersionId.Value, cancellationToken);
        }

        var generated = await proposalCommands.GenerateAsync(
            briefId,
            envelopes.Create(
                tenantId, owner, run.Id, EmailAutomationStageNames.ProposalGenerate,
                0,
                new GenerateProposalCommand(
                    policy.ProposalTitle,
                    [new ProposalOptionInput(
                        planId, policy.ProposalOptionLabel, understanding.Draft.Objective)],
                    policy.ProposalTerms,
                    expiry),
                correlationId),
            cancellationToken);
        return generated.Data;
    }

    private async Task<ProposalVersionView> EnsureApprovedProposalAsync(
        TenantId tenantId,
        ActorId owner,
        EmailAutomationRunRow run,
        Guid planId,
        ProposalVersionView proposal,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        if (proposal.Options.Count != policy.MaximumProposalOptions ||
            proposal.Options.Any(option => option.PlanVersionId != planId ||
                option.Channels.Any(channel =>
                    !policy.AllowedChannels.Contains(channel, StringComparer.Ordinal))))
        {
            throw ReviewRequired(
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
            throw ReviewRequired(
                MasterDataCodes.AutomationFailureReasons.ProposalUnready,
                "The proposal is not approved for automatic delivery.");
        }
        if (proposal.Status == MasterDataCodes.LifecycleStatuses.Sent &&
            !run.DeliveryRequestedAtUtc.HasValue)
        {
            throw DeliveryAmbiguous();
        }
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.Sent &&
            proposal.ExpiryAtUtc <= timeProvider.GetUtcNow())
        {
            throw ReviewRequired(
                MasterDataCodes.AutomationFailureReasons.ProposalUnready,
                "The proposal expired before automatic delivery could start.");
        }
        return proposal;
    }

    private Task<EmailAutomationRunRow> RecordProposalCheckpointAsync(
        EmailAutomationContextRow context,
        Guid proposalId,
        ActorId owner,
        CancellationToken cancellationToken) =>
        store.UpdateRunAsync(
            new TenantId(context.TenantId),
            owner,
            context.InboundEmailId,
            current => current with
            {
                ProposalVersionId = proposalId,
                Checkpoint = MasterDataCodes.EmailAutomationCheckpoints.ProposalApproved,
                UpdatedAtUtc = timeProvider.GetUtcNow(),
            },
            cancellationToken);

    private async Task<ProposalDocumentView> GetOrRenderDocumentAsync(
        TenantId tenantId,
        ActorId owner,
        EmailAutomationRunRow run,
        ProposalVersionView proposal,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        if (proposal.Document is null)
        {
            if (proposal.Status != MasterDataCodes.LifecycleStatuses.Approved)
            {
                throw ReviewRequired(
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
        return proposal.Document ?? throw ReviewRequired(
            MasterDataCodes.AutomationFailureReasons.ProposalUnready,
            "The approved proposal could not be rendered.");
    }

    private Task<EmailAutomationRunRow> RecordDocumentCheckpointAsync(
        EmailAutomationContextRow context,
        Guid documentId,
        ActorId owner,
        CancellationToken cancellationToken) =>
        store.UpdateRunAsync(
            new TenantId(context.TenantId),
            owner,
            context.InboundEmailId,
            current => current with
            {
                DocumentId = documentId,
                Checkpoint = MasterDataCodes.EmailAutomationCheckpoints.DocumentRendered,
                UpdatedAtUtc = timeProvider.GetUtcNow(),
            },
            cancellationToken);

    private static Guid RequireBriefId(EmailAutomationRunRow run) =>
        run.BriefId ?? throw ReviewRequired(
            MasterDataCodes.AutomationFailureReasons.IncompleteBrief,
            "The campaign Brief is not available for proposal creation.");

    private static Guid RequirePlanId(EmailAutomationRunRow run) =>
        run.MediaPlanVersionId ?? throw ReviewRequired(
            MasterDataCodes.AutomationFailureReasons.PlanUnready,
            "The approved media plan is not available for proposal creation.");

    private static EmailAutomationReviewRequiredException ReviewRequired(
        string failureCode,
        string message) =>
        new(failureCode, message);

    private static string BuildDeliveryIdempotencyKey(Guid runId, string recipient) =>
        string.Concat(
            "proposal-email-",
            OpportunityCommandSupport.Hash(
                string.Concat(runId.ToString("N"), ":", recipient.ToLowerInvariant())));
}
