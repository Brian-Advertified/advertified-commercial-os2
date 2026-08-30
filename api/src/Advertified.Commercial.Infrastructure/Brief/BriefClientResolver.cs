using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Brief;

public sealed record BriefClientReference(Guid Id, string Name, bool CreatedForBrief);

public sealed class BriefClientResolver(BriefRecordStore store)
{
    private static readonly string[] ClientAdminRoles =
        [MasterDataCodes.Roles.PlatformAdmin, MasterDataCodes.Roles.AgencyAdmin];

    public async Task<BriefClientReference> ResolveAsync(
        TenantId tenantId,
        ActorId actorId,
        Guid? clientId,
        string? clientName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (clientId.HasValue)
        {
            var existing = await FindByIdAsync(
                tenantId, actorId.Value, clientId.Value, cancellationToken);
            return existing is { CanAccess: true }
                ? new BriefClientReference(existing.Id, existing.Name, false)
                : throw new UnauthorizedAccessException("Brief assignment denied.");
        }

        var name = RequiredName(clientName);
        var matches = await FindByNameAsync(
            tenantId, actorId.Value, name, cancellationToken);
        if (matches.Count > 1)
        {
            throw new ArgumentException(
                "More than one existing client matches the supplied name. Choose the correct client.",
                nameof(clientName));
        }
        if (matches.Count == 1)
        {
            var match = matches[0];
            return match.CanAccess
                ? new BriefClientReference(match.Id, match.Name, false)
                : throw new UnauthorizedAccessException("Brief assignment denied.");
        }
        return await CreateInternalClientAsync(
            tenantId, actorId.Value, name, now, cancellationToken);
    }

    private Task<ClientReferenceRow?> FindByIdAsync(
        TenantId tenantId,
        Guid actorId,
        Guid clientId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<ClientReferenceRow>($"""
            SELECT client.id AS "Id", client.trading_name AS "Name",
                (EXISTS (
                    SELECT 1 FROM commercial.memberships membership
                    WHERE membership.tenant_id = client.tenant_id
                      AND membership.user_id = {actorId}
                      AND membership.status_code = {MasterDataCodes.LifecycleStatuses.Active}
                      AND membership.role_code = ANY({ClientAdminRoles}))
                 OR EXISTS (
                    SELECT 1 FROM commercial.client_account_assignments assignment
                    WHERE assignment.tenant_id = client.tenant_id
                      AND assignment.client_account_id = client.id
                      AND assignment.user_id = {actorId}
                      AND assignment.effective_from_utc <= now()
                      AND (assignment.effective_to_utc IS NULL
                        OR assignment.effective_to_utc > now()))) AS "CanAccess"
            FROM commercial.client_accounts client
            WHERE client.tenant_id = {tenantId.Value} AND client.id = {clientId}
              AND client.status_code = {MasterDataCodes.LifecycleStatuses.Active}
            """).SingleOrDefaultAsync(cancellationToken);

    private Task<List<ClientReferenceRow>> FindByNameAsync(
        TenantId tenantId,
        Guid actorId,
        string name,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<ClientReferenceRow>($"""
            SELECT client.id AS "Id", client.trading_name AS "Name",
                (EXISTS (
                    SELECT 1 FROM commercial.memberships membership
                    WHERE membership.tenant_id = client.tenant_id
                      AND membership.user_id = {actorId}
                      AND membership.status_code = {MasterDataCodes.LifecycleStatuses.Active}
                      AND membership.role_code = ANY({ClientAdminRoles}))
                 OR EXISTS (
                    SELECT 1 FROM commercial.client_account_assignments assignment
                    WHERE assignment.tenant_id = client.tenant_id
                      AND assignment.client_account_id = client.id
                      AND assignment.user_id = {actorId}
                      AND assignment.effective_from_utc <= now()
                      AND (assignment.effective_to_utc IS NULL
                        OR assignment.effective_to_utc > now()))) AS "CanAccess"
            FROM commercial.client_accounts client
            WHERE client.tenant_id = {tenantId.Value}
              AND client.status_code = {MasterDataCodes.LifecycleStatuses.Active}
              AND (lower(trim(client.legal_name)) = lower(trim({name}))
                OR lower(trim(client.trading_name)) = lower(trim({name})))
            ORDER BY client.id
            """).ToListAsync(cancellationToken);

    private async Task<BriefClientReference> CreateInternalClientAsync(
        TenantId tenantId,
        Guid actorId,
        string name,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var clientId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var externalReference = $"brief-{clientId:N}";
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.client_accounts (
                id, tenant_id, external_reference, legal_name, trading_name,
                billing_profile_json, status_code, version, created_at_utc, updated_at_utc)
            VALUES ({clientId}, {tenantId.Value}, {externalReference}, {name}, {name},
                {"{}"}::jsonb, {MasterDataCodes.LifecycleStatuses.Active}, 1, {now}, {now});
            INSERT INTO commercial.client_account_assignments (
                id, tenant_id, client_account_id, user_id, effective_from_utc,
                assigned_by, created_at_utc)
            VALUES ({assignmentId}, {tenantId.Value}, {clientId}, {actorId}, {now},
                {actorId}, {now});
            """, cancellationToken);
        return new BriefClientReference(clientId, name, true);
    }

    private static string RequiredName(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 200)
        {
            throw new ArgumentException(
                "A client or brand name is required to create the Brief.",
                nameof(value));
        }
        return name;
    }

    private sealed record ClientReferenceRow(Guid Id, string Name, bool CanAccess);
}
