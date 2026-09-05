using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventorySupplierLifecycleCommands
{
    private async Task<CommandOutcome> AcceptInvitationOutcomeAsync(
        Guid invitationId,
        CommandEnvelope<AcceptSupplierClaimInvitationCommand> envelope,
        CancellationToken cancellationToken)
    {
        var invitation = await store.FindInvitationAsync(
            envelope.TenantId, invitationId, true, cancellationToken)
            ?? throw new SupplierClaimInvitationInvalidException();
        var now = timeProvider.GetUtcNow();
        ValidateInvitation(invitation, envelope.Command.Token, now);
        var email = await FindActorEmailAsync(envelope, cancellationToken);
        if (!string.Equals(email, invitation.InvitedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new SupplierClaimInvitationInvalidException();
        }

        await EnsureTenantMembershipAsync(invitation, envelope, now, cancellationToken);
        await UpsertSupplierMembershipAsync(invitation, envelope, now, cancellationToken);
        await MarkSupplierClaimedAsync(invitation, envelope, now, cancellationToken);
        await MarkInvitationAcceptedAsync(invitation, envelope, now, cancellationToken);

        var updated = invitation with
        {
            Status = MasterDataCodes.SupplierInvitationStatuses.Accepted,
            AcceptedUserId = envelope.ActorId.Value,
            AcceptedAtUtc = now,
            Version = invitation.Version + 1,
        };
        return OpportunityCommandSupport.Outcome(
            envelope, updated.ToView(), invitation.Id, updated.Version,
            MasterDataReferences.CommercialResourceTypes.SupplierClaimInvitation,
            MasterDataReferences.CommercialActions.SupplierClaimAccepted,
            MasterDataReferences.CommercialEventTypes.SupplierClaimAccepted,
            now);
    }

    private static void ValidateInvitation(
        SupplierClaimInvitationRow invitation,
        string token,
        DateTimeOffset now)
    {
        if (invitation.Status != MasterDataCodes.SupplierInvitationStatuses.Active ||
            invitation.Role != MasterDataCodes.Roles.SupplierUser ||
            invitation.ExpiresAtUtc <= now ||
            !SupplierClaimToken.Matches(token, invitation.TokenHash))
        {
            throw new SupplierClaimInvitationInvalidException();
        }
    }

    private async Task<string> FindActorEmailAsync(
        CommandEnvelope<AcceptSupplierClaimInvitationCommand> envelope,
        CancellationToken cancellationToken)
    {
        var email = await store.InventoryStore.DbContext.Database.SqlQuery<string>($"""
            SELECT email AS "Value"
            FROM commercial.users
            WHERE id = {envelope.ActorId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Active}
            """).SingleOrDefaultAsync(cancellationToken);
        return email ?? throw new SupplierClaimInvitationInvalidException();
    }

    private async Task EnsureTenantMembershipAsync(
        SupplierClaimInvitationRow invitation,
        CommandEnvelope<AcceptSupplierClaimInvitationCommand> envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var membership = await store.InventoryStore.DbContext.Database
            .SqlQuery<TenantMembershipRoleRow>($"""
                SELECT id AS "Id", role_code AS "Role",
                    status_code AS "Status", version AS "Version"
                FROM commercial.memberships
                WHERE tenant_id = {envelope.TenantId.Value}
                  AND user_id = {envelope.ActorId.Value}
                FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);
        if (membership is null)
        {
            await InsertTenantMembershipAsync(invitation, envelope, now, cancellationToken);
            return;
        }
        EnsureExistingTenantMembershipIsEligible(membership);
    }

    private Task<int> InsertTenantMembershipAsync(
        SupplierClaimInvitationRow invitation,
        CommandEnvelope<AcceptSupplierClaimInvitationCommand> envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.InventoryStore.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.memberships (
                id, tenant_id, user_id, role_code, status_code,
                invited_by, invited_at_utc, accepted_at_utc,
                version, created_at_utc, updated_at_utc)
            VALUES ({Guid.NewGuid()}, {envelope.TenantId.Value},
                {envelope.ActorId.Value}, {invitation.Role},
                {MasterDataCodes.LifecycleStatuses.Active},
                {invitation.CreatedBy}, {invitation.CreatedAtUtc}, {now},
                1, {now}, {now})
            """, cancellationToken);

    private static void EnsureExistingTenantMembershipIsEligible(
        TenantMembershipRoleRow membership)
    {
        if (membership.Status != MasterDataCodes.LifecycleStatuses.Active ||
            membership.Role != MasterDataCodes.Roles.SupplierUser)
        {
            throw new SupplierClaimInvitationInvalidException();
        }
    }

    private async Task UpsertSupplierMembershipAsync(
        SupplierClaimInvitationRow invitation,
        CommandEnvelope<AcceptSupplierClaimInvitationCommand> envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existingStatus = await store.InventoryStore.DbContext.Database.SqlQuery<string>($"""
            SELECT status_code AS "Value"
            FROM commercial.inventory_supplier_memberships
            WHERE tenant_id = {envelope.TenantId.Value}
              AND supplier_id = {invitation.SupplierId}
              AND user_id = {envelope.ActorId.Value}
            FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);
        if (existingStatus is not null &&
            existingStatus != MasterDataCodes.LifecycleStatuses.Active)
        {
            throw new SupplierClaimInvitationInvalidException();
        }

        var changed = await store.InventoryStore.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_supplier_memberships (
                id, tenant_id, supplier_id, user_id, role_code,
                status_code, invitation_id, created_by, accepted_at_utc,
                version, created_at_utc, updated_at_utc)
            VALUES ({Guid.NewGuid()}, {envelope.TenantId.Value},
                {invitation.SupplierId}, {envelope.ActorId.Value},
                {invitation.Role}, {MasterDataCodes.LifecycleStatuses.Active},
                {invitation.Id}, {invitation.CreatedBy}, {now}, 1, {now}, {now})
            ON CONFLICT (tenant_id, supplier_id, user_id) DO UPDATE
            SET invitation_id = EXCLUDED.invitation_id,
                accepted_at_utc = EXCLUDED.accepted_at_utc,
                version = commercial.inventory_supplier_memberships.version + 1,
                updated_at_utc = EXCLUDED.updated_at_utc
            """, cancellationToken);
        if (changed != 1) throw new SupplierClaimInvitationInvalidException();
    }

    private Task<int> MarkSupplierClaimedAsync(
        SupplierClaimInvitationRow invitation,
        CommandEnvelope<AcceptSupplierClaimInvitationCommand> envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.InventoryStore.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_suppliers
            SET claim_status_code = {MasterDataCodes.SupplierClaimStatuses.Claimed},
                claimed_by = COALESCE(claimed_by, {envelope.ActorId.Value}),
                claimed_at_utc = COALESCE(claimed_at_utc, {now}),
                version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value}
              AND id = {invitation.SupplierId}
            """, cancellationToken);

    private async Task MarkInvitationAcceptedAsync(
        SupplierClaimInvitationRow invitation,
        CommandEnvelope<AcceptSupplierClaimInvitationCommand> envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await store.InventoryStore.DbContext.Database
            .ExecuteSqlInterpolatedAsync($"""
                UPDATE commercial.supplier_claim_invitations
                SET status_code =
                        {MasterDataCodes.SupplierInvitationStatuses.Accepted},
                    accepted_user_id = {envelope.ActorId.Value},
                    accepted_at_utc = {now}, version = version + 1
                WHERE tenant_id = {envelope.TenantId.Value}
                  AND id = {invitation.Id}
                  AND status_code =
                        {MasterDataCodes.SupplierInvitationStatuses.Active}
                  AND version = {invitation.Version}
                """, cancellationToken);
        if (changed != 1)
        {
            throw new SupplierClaimInvitationInvalidException();
        }
    }
}
