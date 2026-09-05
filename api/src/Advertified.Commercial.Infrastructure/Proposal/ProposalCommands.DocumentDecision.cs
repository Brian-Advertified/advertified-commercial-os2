using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed partial class ProposalCommands
{
    private async Task<CommandOutcome> RenderOutcomeAsync(
        Guid proposalVersionId,
        CommandEnvelope<RenderProposalCommand> envelope,
        CancellationToken cancellationToken)
    {
        var proposal = await LoadOwnedProposalAsync(proposalVersionId, envelope, cancellationToken);
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.Approved ||
            proposal.ExpiryAtUtc <= timeProvider.GetUtcNow() ||
            await store.FindDocumentAsync(envelope.TenantId, proposalVersionId, cancellationToken) is not null)
        {
            throw new InvalidLifecycleTransitionException();
        }
        await inventoryReadiness.EnsureProposalPlansCurrentAsync(envelope.TenantId, proposalVersionId, cancellationToken);
        var view = await store.BuildViewAsync(envelope.TenantId, proposal, cancellationToken);
        var rendered = ProposalPdfRenderer.Render(view);
        var documentId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.proposal_documents (
                id, tenant_id, proposal_version_id, media_type, file_name,
                content_hash, content, created_at_utc)
            VALUES ({documentId}, {envelope.TenantId.Value}, {proposalVersionId},
                {"application/pdf"}, {rendered.FileName}, {rendered.ContentHash},
                {rendered.Content}, {now})
            """, cancellationToken);
        var changed = await IncrementVersionAsync(proposal, envelope, cancellationToken);
        var updated = proposal with { Version = changed };
        var result = await store.BuildViewAsync(envelope.TenantId, updated, cancellationToken);
        return ProposalOutcome(envelope, result, proposalVersionId, changed,
            MasterDataReferences.CommercialActions.ProposalRendered,
            MasterDataReferences.CommercialEventTypes.ProposalRendered, now);
    }

    private async Task<CommandOutcome> ShareOutcomeAsync(
        Guid proposalVersionId,
        CommandEnvelope<ShareProposalCommand> envelope,
        EmailDeliveryReceipt receipt,
        CancellationToken cancellationToken)
    {
        var proposal = await LoadOwnedProposalAsync(proposalVersionId, envelope, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.Approved || proposal.ExpiryAtUtc <= now ||
            await store.FindDocumentAsync(envelope.TenantId, proposalVersionId, cancellationToken) is null)
        {
            throw new ProposalDocumentRequiredException();
        }
        await inventoryReadiness.EnsureProposalPlansCurrentAsync(envelope.TenantId, proposalVersionId, cancellationToken);
        var recipient = await store.FindRecipientAsync(
            envelope.TenantId, envelope.Command.RecipientUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Client recipient is unavailable.");
        if (recipient.Status != MasterDataCodes.LifecycleStatuses.Active ||
            recipient.Role is not (MasterDataCodes.Roles.AdvertiserAdmin or MasterDataCodes.Roles.AdvertiserApprover))
        {
            throw new UnauthorizedAccessException("Client recipient is unavailable.");
        }
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.proposal_versions
            SET status_code = {MasterDataCodes.LifecycleStatuses.Sent},
                recipient_user_id = {envelope.Command.RecipientUserId},
                shared_at_utc = {receipt.AcceptedAtUtc}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {proposalVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Approved}
              AND inventory_review_status_code =
                    {MasterDataCodes.ProposalInventoryReviewStatuses.Current}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        var updated = proposal with
        {
            Status = MasterDataCodes.LifecycleStatuses.Sent,
            RecipientUserId = envelope.Command.RecipientUserId,
            Version = proposal.Version + 1,
        };
        var view = await store.BuildViewAsync(envelope.TenantId, updated, cancellationToken);
        return ProposalOutcome(envelope, view, proposalVersionId, updated.Version,
            MasterDataReferences.CommercialActions.ProposalShared,
            MasterDataReferences.CommercialEventTypes.ProposalShared, receipt.AcceptedAtUtc);
    }

    private async Task<CommandOutcome> RecordExternalDecisionOutcomeAsync(
        Guid proposalVersionId,
        CommandEnvelope<RecordExternalProposalDecisionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var proposal = await store.FindProposalAsync(
            envelope.TenantId, proposalVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Proposal access denied.");
        var now = timeProvider.GetUtcNow();
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.Sent ||
            proposal.RecipientUserId is not null)
        {
            throw new InvalidLifecycleTransitionException();
        }
        if (proposal.ExpiryAtUtc <= now)
        {
            throw new ProposalExpiredException();
        }
        if (envelope.Command.Declined == envelope.Command.OptionId.HasValue)
        {
            throw new ArgumentException(
                "Choose one proposal option or record that the client declined.");
        }
        var authority = await store.FindExternalDecisionAuthorityAsync(
            envelope.TenantId, proposalVersionId, envelope.ActorId.Value,
            cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Only the owner of the exact sent Rapid OOH run may record this reply.");
        if (await store.FindDecisionAsync(
                envelope.TenantId, proposalVersionId, cancellationToken) is not null)
        {
            throw new InvalidLifecycleTransitionException();
        }
        if (!envelope.Command.Declined)
        {
            EnsureProposalInventoryCurrent(proposal);
        }
        if (envelope.Command.OptionId.HasValue)
        {
            var options = await store.ListOptionsAsync(
                envelope.TenantId, proposalVersionId, cancellationToken);
            if (options.All(item => item.Id != envelope.Command.OptionId.Value))
            {
                throw new ArgumentException("The selected proposal choice is unavailable.");
            }
        }
        var evidenceReference = Required(
            envelope.Command.EvidenceReference, 1000,
            nameof(envelope.Command.EvidenceReference));
        var reason = string.IsNullOrWhiteSpace(envelope.Command.Reason)
            ? null
            : envelope.Command.Reason.Trim();
        if (reason is { Length: > 1000 })
        {
            throw new ArgumentException("The decision reason is too long.");
        }
        var decision = envelope.Command.Declined
            ? MasterDataCodes.LifecycleStatuses.Declined
            : MasterDataCodes.LifecycleStatuses.Selected;
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.proposal_decisions (
                id, tenant_id, proposal_version_id, option_id, decision_code,
                reason, decided_by, decided_at_utc, recorded_for_external_party,
                external_party_email, evidence_reference)
            VALUES ({Guid.NewGuid()}, {envelope.TenantId.Value}, {proposalVersionId},
                {envelope.Command.OptionId}, {decision}, {reason},
                {envelope.ActorId.Value}, {now}, TRUE,
                {authority.ExternalPartyEmail}, {evidenceReference})
            """, cancellationToken);
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.proposal_versions
            SET status_code = {decision}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {proposalVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Sent}
              AND recipient_user_id IS NULL
              AND ({envelope.Command.Declined} OR inventory_review_status_code =
                    {MasterDataCodes.ProposalInventoryReviewStatuses.Current})
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        var updated = proposal with
        {
            Status = decision,
            Version = proposal.Version + 1,
        };
        var view = await store.BuildViewAsync(
            envelope.TenantId, updated, cancellationToken);
        return ProposalOutcome(
            envelope,
            view,
            proposalVersionId,
            updated.Version,
            envelope.Command.Declined
                ? MasterDataReferences.CommercialActions.ProposalDeclined
                : MasterDataReferences.CommercialActions.ProposalSelected,
            envelope.Command.Declined
                ? MasterDataReferences.CommercialEventTypes.ProposalDeclined
                : MasterDataReferences.CommercialEventTypes.ProposalOptionSelected,
            now);
    }

    private Task<CommandOutcome> SelectOutcomeAsync(
        Guid proposalVersionId,
        CommandEnvelope<SelectProposalOptionCommand> envelope,
        CancellationToken cancellationToken) => DecideAsync(
            proposalVersionId, envelope, MasterDataCodes.LifecycleStatuses.Selected,
            envelope.Command.OptionId, envelope.Command.Reason,
            MasterDataReferences.CommercialActions.ProposalSelected,
            MasterDataReferences.CommercialEventTypes.ProposalOptionSelected,
            cancellationToken);

    private Task<CommandOutcome> DeclineOutcomeAsync(
        Guid proposalVersionId,
        CommandEnvelope<DeclineProposalCommand> envelope,
        CancellationToken cancellationToken) => DecideAsync(
            proposalVersionId, envelope, MasterDataCodes.LifecycleStatuses.Declined,
            null, envelope.Command.Reason,
            MasterDataReferences.CommercialActions.ProposalDeclined,
            MasterDataReferences.CommercialEventTypes.ProposalDeclined,
            cancellationToken);

    private async Task<CommandOutcome> DecideAsync<TCommand>(
        Guid proposalVersionId,
        CommandEnvelope<TCommand> envelope,
        string decision,
        Guid? optionId,
        string? reason,
        ActionCode action,
        EventTypeCode eventType,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var proposal = await store.FindProposalAsync(
            envelope.TenantId, proposalVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Proposal access denied.");
        var now = timeProvider.GetUtcNow();
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.Sent ||
            proposal.RecipientUserId != envelope.ActorId.Value)
        {
            throw new UnauthorizedAccessException("This proposal decision is not assigned to you.");
        }
        if (proposal.ExpiryAtUtc <= now) throw new ProposalExpiredException();
        if (decision == MasterDataCodes.LifecycleStatuses.Selected)
        {
            EnsureProposalInventoryCurrent(proposal);
        }
        if (await store.FindDecisionAsync(envelope.TenantId, proposalVersionId, cancellationToken) is not null)
        {
            throw new InvalidLifecycleTransitionException();
        }
        if (optionId.HasValue)
        {
            var options = await store.ListOptionsAsync(envelope.TenantId, proposalVersionId, cancellationToken);
            if (options.All(item => item.Id != optionId.Value))
            {
                throw new ArgumentException("The selected proposal choice is unavailable.");
            }
        }
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.proposal_decisions (
                id, tenant_id, proposal_version_id, option_id, decision_code,
                reason, decided_by, decided_at_utc)
            VALUES ({Guid.NewGuid()}, {envelope.TenantId.Value}, {proposalVersionId}, {optionId},
                {decision}, {reason}, {envelope.ActorId.Value}, {now})
            """, cancellationToken);
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.proposal_versions
            SET status_code = {decision}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {proposalVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Sent}
              AND ({decision} = {MasterDataCodes.LifecycleStatuses.Declined}
                   OR inventory_review_status_code =
                        {MasterDataCodes.ProposalInventoryReviewStatuses.Current})
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        var updated = proposal with { Status = decision, Version = proposal.Version + 1 };
        var view = await store.BuildViewAsync(envelope.TenantId, updated, cancellationToken);
        return ProposalOutcome(envelope, view, proposalVersionId, updated.Version, action, eventType, now);
    }

    private async Task<long> IncrementVersionAsync<TCommand>(
        ProposalRow proposal,
        CommandEnvelope<TCommand> envelope,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.proposal_versions SET version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {proposal.Id}
              AND inventory_review_status_code =
                    {MasterDataCodes.ProposalInventoryReviewStatuses.Current}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        return proposal.Version + 1;
    }
}
