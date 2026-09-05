using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class SupplierClaimAcceptancePolicy
{
    internal static async Task AuthorizeAsync(InventorySupplierLifecycleStore store, Guid invitationId,
        CommandEnvelope<AcceptSupplierClaimInvitationCommand> envelope, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invitation = await store.FindInvitationAsync(envelope.TenantId, invitationId, true, cancellationToken)
            ?? throw new SupplierClaimInvitationInvalidException();
        var accepted = invitation.Status == MasterDataCodes.SupplierInvitationStatuses.Accepted;
        if (invitation.Role != MasterDataCodes.Roles.SupplierUser ||
            !SupplierClaimToken.Matches(envelope.Command.Token, invitation.TokenHash) ||
            (accepted ? invitation.AcceptedUserId != envelope.ActorId.Value :
                invitation.Status != MasterDataCodes.SupplierInvitationStatuses.Active || invitation.ExpiresAtUtc <= now))
            throw new SupplierClaimInvitationInvalidException();
        var eligible = await store.InventoryStore.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.users actor
                JOIN commercial.tenants tenant ON tenant.id = {envelope.TenantId.Value}
                    AND tenant.status_code = {MasterDataCodes.LifecycleStatuses.Active}
                JOIN commercial.inventory_suppliers supplier ON supplier.id = {invitation.SupplierId}
                    AND supplier.tenant_id = {envelope.TenantId.Value}
                JOIN governance.master_data_items role ON role.collection_code = {MasterDataCodes.Roles.Collection}
                    AND role.code = {MasterDataCodes.Roles.SupplierUser} AND role.is_active
                LEFT JOIN commercial.memberships tenant_member
                    ON tenant_member.tenant_id = supplier.tenant_id AND tenant_member.user_id = actor.id
                LEFT JOIN commercial.inventory_supplier_memberships supplier_member
                    ON supplier_member.tenant_id = supplier.tenant_id
                    AND supplier_member.supplier_id = supplier.id AND supplier_member.user_id = actor.id
                WHERE actor.id = {envelope.ActorId.Value}
                    AND actor.status_code = {MasterDataCodes.LifecycleStatuses.Active}
                    AND lower(actor.email) = lower({invitation.InvitedEmail})
                    AND (tenant_member.id IS NULL AND NOT {accepted} OR
                        tenant_member.status_code = {MasterDataCodes.LifecycleStatuses.Active}
                        AND tenant_member.role_code = {MasterDataCodes.Roles.SupplierUser})
                    AND (supplier_member.id IS NULL AND NOT {accepted} OR
                        supplier_member.status_code = {MasterDataCodes.LifecycleStatuses.Active}
                        AND supplier_member.role_code = {MasterDataCodes.Roles.SupplierUser})
            ) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!eligible) throw new SupplierClaimInvitationInvalidException();
    }
}
