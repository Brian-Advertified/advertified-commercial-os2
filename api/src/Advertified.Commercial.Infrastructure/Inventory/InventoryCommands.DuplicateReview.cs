using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands
{
    private async Task<CommandOutcome> ReviewDuplicateOutcomeAsync(
        Guid candidateId,
        CommandEnvelope<ReviewInventoryDuplicateCommand> envelope,
        CancellationToken cancellationToken)
    {
        var candidate = await LoadDuplicateAsync(
            envelope, candidateId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Duplicate candidate access denied.");
        if (candidate.Status != MasterDataCodes.InventoryDuplicateStatuses.Open)
            throw new InvalidLifecycleTransitionException();
        if (candidate.Version != envelope.ExpectedVersion) throw new VersionConflictException();
        var decision = envelope.Command.Decision?.Trim().ToUpperInvariant();
        if (decision is not (MasterDataCodes.InventoryDuplicateStatuses.ConfirmedSameIdentity or
            MasterDataCodes.InventoryDuplicateStatuses.Dismissed or
            MasterDataCodes.InventoryDuplicateStatuses.Deferred))
        {
            throw new ArgumentException("Select a supported duplicate review decision.");
        }
        var reason = OpportunityCommandSupport.Required(
            envelope.Command.Reason, 2_000, nameof(envelope.Command.Reason));
        var canonicalId = decision == MasterDataCodes.InventoryDuplicateStatuses.ConfirmedSameIdentity
            ? envelope.Command.CanonicalProductId
            : null;
        if ((canonicalId.HasValue && canonicalId != candidate.LeftProductId &&
                canonicalId != candidate.RightProductId) ||
            (decision == MasterDataCodes.InventoryDuplicateStatuses.ConfirmedSameIdentity &&
                !canonicalId.HasValue) ||
            (decision != MasterDataCodes.InventoryDuplicateStatuses.ConfirmedSameIdentity &&
                envelope.Command.CanonicalProductId.HasValue))
        {
            throw new ArgumentException("The canonical product selection is invalid.");
        }
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_duplicate_candidates
            SET status_code = {decision}, canonical_product_id = {canonicalId},
                reviewed_by = {envelope.ActorId.Value}, reviewed_at_utc = {now},
                review_reason = {reason}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {candidateId}
              AND status_code = {MasterDataCodes.InventoryDuplicateStatuses.Open}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        if (canonicalId.HasValue)
        {
            var duplicateId = canonicalId == candidate.LeftProductId
                ? candidate.RightProductId : candidate.LeftProductId;
            await EnsureIdentityLinkAllowedAsync(
                envelope.TenantId, canonicalId.Value, duplicateId, cancellationToken);
            await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.inventory_product_identity_links (
                    id, tenant_id, duplicate_product_id, canonical_product_id,
                    duplicate_candidate_id, linked_by, linked_at_utc)
                VALUES ({Guid.NewGuid()}, {envelope.TenantId.Value}, {duplicateId},
                    {canonicalId}, {candidateId}, {envelope.ActorId.Value}, {now})
                """, cancellationToken);
        }
        var updated = await LoadDuplicateAsync(
            envelope, candidateId, false, cancellationToken)
            ?? throw new InvalidOperationException("Duplicate review was not persisted.");
        return OpportunityCommandSupport.Outcome(
            envelope, updated.ToView(), candidateId, updated.Version,
            MasterDataReferences.CommercialResourceTypes.InventoryDuplicateCandidate,
            MasterDataReferences.CommercialActions.InventoryDuplicateReviewed,
            MasterDataReferences.CommercialEventTypes.InventoryDuplicateReviewed, now);
    }

    private async Task EnsureIdentityLinkAllowedAsync(
        TenantId tenantId,
        Guid canonicalId,
        Guid duplicateId,
        CancellationToken cancellationToken)
    {
        await store.DbContext.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM commercial.inventory_products
            WHERE tenant_id = {tenantId.Value}
              AND id = ANY({new[] { canonicalId, duplicateId }})
            ORDER BY id FOR UPDATE
            """).ToListAsync(cancellationToken);
        var conflicts = await store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.inventory_product_identity_links identity_link
                WHERE identity_link.tenant_id = {tenantId.Value}
                  AND (identity_link.duplicate_product_id IN ({canonicalId}, {duplicateId})
                    OR identity_link.canonical_product_id = {duplicateId})
            ) AS "Value"
            """).SingleAsync(cancellationToken);
        if (conflicts) throw new InvalidLifecycleTransitionException();
    }

    private Task<InventoryDuplicateCandidateRow?> LoadDuplicateAsync<TCommand>(
        CommandEnvelope<TCommand> envelope,
        Guid candidateId,
        bool forUpdate,
        CancellationToken cancellationToken) where TCommand : notnull
    {
        var suffix = forUpdate ? " FOR UPDATE OF candidate" : string.Empty;
        return store.DbContext.Database.SqlQuery<InventoryDuplicateCandidateRow>(
            FormattableStringFactory.Create(
                InventoryDuplicateQueries.Select + " WHERE candidate.tenant_id = {0} " +
                "AND candidate.id = {1}" + suffix,
                envelope.TenantId.Value, candidateId)).SingleOrDefaultAsync(cancellationToken);
    }
}
