using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventorySupplierLifecycleCommands
{
    private async Task<CommandOutcome> IssueInvitationOutcomeAsync(
        Guid supplierId,
        CommandEnvelope<IssueSupplierClaimInvitationCommand> envelope,
        CancellationToken cancellationToken)
    {
        var email = new EmailAddress(envelope.Command.Email).Value;
        var role = ValidateSupplierRole(envelope.Command.Role);
        var validForDays = ValidateValidityDays(envelope.Command.ValidForDays);
        var supplier = await store.FindSupplierAsync(
            envelope.TenantId, supplierId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Supplier access denied.");
        if (supplier.Version != envelope.ExpectedVersion)
        {
            throw new VersionConflictException();
        }

        var now = timeProvider.GetUtcNow();
        await ExpireInvitationsAsync(envelope.TenantId, now, cancellationToken);
        await RevokeExistingInvitationAsync(
            envelope, supplierId, email, now, cancellationToken);
        var invitation = await InsertInvitationAsync(
            envelope, supplier, email, role, validForDays, now,
            cancellationToken);
        var changed = await MarkSupplierInvitedAsync(
            envelope, supplier, now, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }

        var durableView = invitation with { RegistrationToken = null };
        var durablePayload = JsonSerializer.SerializeToElement(durableView);
        return OpportunityCommandSupport.Outcome(
            envelope, invitation,
            supplier.Id, supplier.Version + 1,
            MasterDataReferences.CommercialResourceTypes.InventorySupplier,
            MasterDataReferences.CommercialActions.SupplierClaimInvitationIssued,
            MasterDataReferences.CommercialEventTypes.SupplierClaimInvitationIssued,
            now,
            durablePayload,
            durablePayload);
    }

    private async Task<SupplierClaimInvitationView> InsertInvitationAsync(
        CommandEnvelope<IssueSupplierClaimInvitationCommand> envelope,
        InventorySupplierLifecycleRow supplier,
        string email,
        string role,
        int validForDays,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var (token, tokenHash) = SupplierClaimToken.Create();
        var id = Guid.NewGuid();
        var expires = now.AddDays(validForDays);
        await store.InventoryStore.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.supplier_claim_invitations (
                id, tenant_id, supplier_id, invited_email, invited_role_code,
                token_hash, status_code, expires_at_utc, created_by,
                created_at_utc, version)
            VALUES ({id}, {envelope.TenantId.Value}, {supplier.Id}, {email}, {role},
                {tokenHash}, {MasterDataCodes.SupplierInvitationStatuses.Active},
                {expires}, {envelope.ActorId.Value}, {now}, 1)
            """, cancellationToken);
        return new SupplierClaimInvitationView(
            id, supplier.Id, supplier.Name, email, role,
            MasterDataCodes.SupplierInvitationStatuses.Active,
            expires, token, envelope.ActorId.Value, now, null, null, 1);
    }

    private Task<int> MarkSupplierInvitedAsync(
        CommandEnvelope<IssueSupplierClaimInvitationCommand> envelope,
        InventorySupplierLifecycleRow supplier,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.InventoryStore.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_suppliers
            SET claim_status_code = CASE
                    WHEN claim_status_code =
                        {MasterDataCodes.SupplierClaimStatuses.Claimed}
                    THEN claim_status_code
                    ELSE {MasterDataCodes.SupplierClaimStatuses.Invited}
                END,
                version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {supplier.Id}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);

    private Task<int> ExpireInvitationsAsync(
        TenantId tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.InventoryStore.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.supplier_claim_invitations
            SET status_code = {MasterDataCodes.SupplierInvitationStatuses.Expired},
                version = version + 1
            WHERE tenant_id = {tenantId.Value}
              AND status_code = {MasterDataCodes.SupplierInvitationStatuses.Active}
              AND expires_at_utc <= {now}
            """, cancellationToken);

    private Task<int> RevokeExistingInvitationAsync(
        CommandEnvelope<IssueSupplierClaimInvitationCommand> envelope,
        Guid supplierId,
        string email,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.InventoryStore.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.supplier_claim_invitations
            SET status_code = {MasterDataCodes.SupplierInvitationStatuses.Revoked},
                revoked_by = {envelope.ActorId.Value}, revoked_at_utc = {now},
                revocation_reason = {"Replaced by a newer registration invitation."},
                version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value}
              AND supplier_id = {supplierId}
              AND lower(invited_email) = {email}
              AND status_code = {MasterDataCodes.SupplierInvitationStatuses.Active}
            """, cancellationToken);

    private static int ValidateValidityDays(int validForDays) =>
        validForDays is >= 1 and <= 30
            ? validForDays
            : throw new ArgumentOutOfRangeException(
                nameof(validForDays), validForDays,
                "The registration invitation must expire within 1 to 30 days.");

    private static string ValidateSupplierRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        var normalized = role.Trim();
        return normalized == MasterDataCodes.Roles.SupplierUser
            ? normalized
            : throw new ArgumentException("The supplier user role is required.", nameof(role));
    }
}
