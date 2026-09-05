using System.Security.Cryptography;
using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Advertified.Commercial.Infrastructure.Proposal;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed class ProposalEmailIntentStore(
    ProposalRecordStore proposals, ProposalInventoryReadiness readiness, TimeProvider clock)
{
    internal async Task<(ProposalEmailIntentRow Row, bool IsNew)> PrepareAsync(
        ProposalEmailBinding binding, ProposalEmailDelivery delivery, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            binding.ActorId, binding.DocumentId, binding.ExpectedVersion, binding.ProviderCode,
            delivery.To, delivery.From, delivery.Subject, delivery.TextBody, delivery.FileName,
            delivery.MediaType, delivery.InReplyTo, delivery.IdempotencyKey,
            AttachmentHash = Convert.ToHexStringLower(SHA256.HashData(delivery.Attachment)),
        });
        var hash = OpportunityCommandSupport.Hash(payload);
        await using var transaction = await proposals.BeginSessionAsync(
            binding.ActorId, binding.TenantId, cancellationToken);
        if (binding.CommandIdentity is { } identity)
            await identity.ReserveAsync(proposals.DbContext, binding.ActorId, clock.GetUtcNow(), cancellationToken);
        await LockProposalAsync(binding, cancellationToken);
        var previous = await FindAsync(binding, cancellationToken);
        if (previous is not null)
        {
            if (previous.ActorId != binding.ActorId.Value || previous.PayloadHash != hash)
                throw new IdempotencyConflictException();
            await transaction.CommitAsync(cancellationToken);
            return (previous, false);
        }
        await ValidateNewIntentAsync(binding, delivery, cancellationToken);
        var id = Guid.NewGuid();
        await proposals.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.proposal_email_deliveries (
                id, tenant_id, actor_id, proposal_version_id, document_id,
                original_version, provider_code, idempotency_key, payload_hash,
                payload_json, requested_at_utc)
            VALUES ({id}, {binding.TenantId.Value}, {binding.ActorId.Value},
                {binding.ProposalId}, {binding.DocumentId}, {binding.ExpectedVersion},
                {binding.ProviderCode}, {delivery.IdempotencyKey}, {hash},
                {payload}::jsonb, {clock.GetUtcNow()})
            """, cancellationToken);
        var row = await FindAsync(binding, cancellationToken)
            ?? throw new InvalidOperationException("The delivery intent was not retained.");
        await transaction.CommitAsync(cancellationToken);
        return (row, true);
    }

    private Task<Guid> LockProposalAsync(ProposalEmailBinding binding, CancellationToken cancellationToken) =>
        proposals.DbContext.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM commercial.proposal_versions
            WHERE tenant_id = {binding.TenantId.Value} AND id = {binding.ProposalId}
            FOR UPDATE
            """).SingleAsync(cancellationToken);

    private async Task ValidateNewIntentAsync(
        ProposalEmailBinding binding, ProposalEmailDelivery delivery, CancellationToken cancellationToken)
    {
        var proposal = await proposals.FindProposalAsync(binding.TenantId, binding.ProposalId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Proposal access denied.");
        var brief = await proposals.FindPlanningReadyBriefAsync(binding.TenantId, proposal.BriefId, cancellationToken);
        if (brief?.OwnerUserId != binding.ActorId.Value || brief.BriefVersionId != proposal.BriefVersionId)
            throw new UnauthorizedAccessException("Proposal assignment denied.");
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.Approved ||
            proposal.Version != binding.ExpectedVersion || proposal.ExpiryAtUtc <= clock.GetUtcNow())
            throw new ProposalStaleException();
        if (proposal.InventoryReviewStatus != MasterDataCodes.ProposalInventoryReviewStatuses.Current)
            throw new ProposalInventoryReviewRequiredException();
        await readiness.EnsureProposalPlansCurrentAsync(binding.TenantId, binding.ProposalId, cancellationToken);
        var document = await proposals.FindDocumentAsync(binding.TenantId, binding.ProposalId, cancellationToken);
        if (document is null || document.Id != binding.DocumentId || document.MediaType != delivery.MediaType ||
            document.FileName != delivery.FileName || !document.Content.AsSpan().SequenceEqual(delivery.Attachment))
            throw new ProposalDocumentRequiredException();
    }

    internal async Task RecordAcceptanceAsync(
        ProposalEmailBinding binding, EmailDeliveryReceipt receipt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(receipt.ProviderMessageId) || receipt.ProviderMessageId.Length > 300)
            throw new EmailDeliveryAcceptanceUnknownException();
        await using var transaction = await proposals.BeginSessionAsync(binding.ActorId, binding.TenantId, cancellationToken);
        var changed = await proposals.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.proposal_email_deliveries
            SET provider_message_id = {receipt.ProviderMessageId}, accepted_at_utc = {receipt.AcceptedAtUtc}
            WHERE tenant_id = {binding.TenantId.Value} AND proposal_version_id = {binding.ProposalId}
              AND actor_id = {binding.ActorId.Value} AND provider_code = {binding.ProviderCode}
              AND (provider_message_id IS NULL OR
                   (provider_message_id = {receipt.ProviderMessageId} AND accepted_at_utc = {receipt.AcceptedAtUtc}))
            """, cancellationToken);
        if (changed != 1) throw new EmailDeliveryAcceptanceUnknownException();
        await transaction.CommitAsync(cancellationToken);
    }

    internal async Task<EmailDeliveryReceipt?> ReadAcceptanceAsync(
        TenantId tenantId, ActorId actorId, Guid proposalId, string providerCode,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        await using var transaction = await proposals.BeginSessionAsync(actorId, tenantId, cancellationToken);
        var receipt = await proposals.DbContext.Database.SqlQuery<EmailDeliveryReceipt>($"""
            SELECT provider_message_id AS "ProviderMessageId", accepted_at_utc AS "AcceptedAtUtc"
            FROM commercial.proposal_email_deliveries
            WHERE tenant_id = {tenantId.Value} AND actor_id = {actorId.Value}
                AND proposal_version_id = {proposalId} AND provider_code = {providerCode}
                AND idempotency_key = {idempotencyKey} AND accepted_at_utc IS NOT NULL
            """).SingleOrDefaultAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return receipt;
    }

    private Task<ProposalEmailIntentRow?> FindAsync(ProposalEmailBinding binding, CancellationToken cancellationToken) =>
        proposals.DbContext.Database.SqlQuery<ProposalEmailIntentRow>($"""
            SELECT id AS "Id", actor_id AS "ActorId", payload_hash AS "PayloadHash",
                provider_message_id AS "ProviderMessageId", accepted_at_utc AS "AcceptedAtUtc"
            FROM commercial.proposal_email_deliveries
            WHERE tenant_id = {binding.TenantId.Value} AND proposal_version_id = {binding.ProposalId}
            """).SingleOrDefaultAsync(cancellationToken);
}

internal sealed record ProposalEmailBinding(
    TenantId TenantId, ActorId ActorId, Guid ProposalId, Guid DocumentId,
    long ExpectedVersion, string ProviderCode, CommandIntentIdentity? CommandIdentity = null);

internal sealed record ProposalEmailIntentRow(
    Guid Id, Guid ActorId, string PayloadHash, string? ProviderMessageId, DateTimeOffset? AcceptedAtUtc);
