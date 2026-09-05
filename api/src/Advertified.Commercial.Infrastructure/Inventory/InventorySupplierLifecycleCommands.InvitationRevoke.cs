using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventorySupplierLifecycleCommands
{
    private async Task<CommandOutcome> RevokeInvitationOutcomeAsync(
        Guid invitationId,
        CommandEnvelope<RevokeSupplierClaimInvitationCommand> envelope,
        CancellationToken cancellationToken)
    {
        var reason = OpportunityCommandSupport.Required(
            envelope.Command.Reason, 1000, nameof(envelope.Command.Reason));
        var invitation = await store.FindInvitationAsync(
            envelope.TenantId, invitationId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invitation access denied.");
        var now = timeProvider.GetUtcNow();
        if (invitation.Version != envelope.ExpectedVersion)
        {
            throw new VersionConflictException();
        }
        if (invitation.Status != MasterDataCodes.SupplierInvitationStatuses.Active ||
            invitation.ExpiresAtUtc <= now)
        {
            throw new SupplierClaimInvitationInvalidException();
        }

        await PersistInvitationRevocationAsync(
            invitation, envelope, reason, now, cancellationToken);
        await RestoreUnclaimedSupplierWhenNeededAsync(
            invitation.SupplierId, envelope.TenantId, now, cancellationToken);
        var updated = invitation with
        {
            Status = MasterDataCodes.SupplierInvitationStatuses.Revoked,
            RevokedBy = envelope.ActorId.Value,
            RevokedAtUtc = now,
            RevocationReason = reason,
            Version = invitation.Version + 1,
        };
        var view = updated.ToView();
        return OpportunityCommandSupport.Outcome(
            envelope, view, invitation.Id, updated.Version,
            MasterDataReferences.CommercialResourceTypes.SupplierClaimInvitation,
            MasterDataReferences.CommercialActions.SupplierClaimInvitationRevoked,
            MasterDataReferences.CommercialEventTypes.SupplierClaimInvitationRevoked,
            now);
    }

    private async Task PersistInvitationRevocationAsync(
        SupplierClaimInvitationRow invitation,
        CommandEnvelope<RevokeSupplierClaimInvitationCommand> envelope,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await store.InventoryStore.DbContext.Database
            .ExecuteSqlInterpolatedAsync($"""
                UPDATE commercial.supplier_claim_invitations
                SET status_code =
                        {MasterDataCodes.SupplierInvitationStatuses.Revoked},
                    revoked_by = {envelope.ActorId.Value},
                    revoked_at_utc = {now}, revocation_reason = {reason},
                    version = version + 1
                WHERE tenant_id = {envelope.TenantId.Value}
                  AND id = {invitation.Id}
                  AND status_code =
                        {MasterDataCodes.SupplierInvitationStatuses.Active}
                  AND version = {envelope.ExpectedVersion}
                """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
    }

    private Task<int> RestoreUnclaimedSupplierWhenNeededAsync(
        Guid supplierId,
        TenantId tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.InventoryStore.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_suppliers supplier
            SET claim_status_code =
                    {MasterDataCodes.SupplierClaimStatuses.Unclaimed},
                version = version + 1, updated_at_utc = {now}
            WHERE supplier.tenant_id = {tenantId.Value}
              AND supplier.id = {supplierId}
              AND supplier.claim_status_code =
                    {MasterDataCodes.SupplierClaimStatuses.Invited}
              AND NOT EXISTS (
                  SELECT 1
                  FROM commercial.supplier_claim_invitations invitation
                  WHERE invitation.tenant_id = supplier.tenant_id
                    AND invitation.supplier_id = supplier.id
                    AND invitation.status_code =
                        {MasterDataCodes.SupplierInvitationStatuses.Active})
            """, cancellationToken);
}
