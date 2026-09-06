using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Identity;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Onboarding;

public sealed class PublicIntakeProvisioner(PublicIntakeStore store)
{
    private const string EmptyJson = "{}";

    internal async Task<PublicIntakeProvisioningResult> ProvisionAsync(
        PublicIntakeRow row,
        ActorId reviewer,
        TenantId platformTenantId,
        string legalName,
        string tradingName,
        string? website,
        string? vatNumber,
        bool requireMfa,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var profile = PublicIntakePolicy.ProvisioningFor(row.TypeCode);
        var userId = await UserLoginDirectory.EnsureProvisionedUserAsync(
            store.DbContext,
            reviewer,
            platformTenantId,
            row.Email,
            row.Name,
            row.Phone,
            requireMfa,
            now,
            cancellationToken);
        var tenantId = Guid.NewGuid();

        await ApplicationDatabaseSession.SetAsync(
            store.DbContext,
            new UserId(userId),
            new TenantId(tenantId),
            cancellationToken);
        await InsertTenantAsync(
            tenantId,
            profile,
            legalName,
            tradingName,
            vatNumber,
            BuildSlug(tradingName, row.Id),
            now,
            cancellationToken);
        await InsertMembershipAsync(
            tenantId,
            userId,
            reviewer.Value,
            profile.Role,
            now,
            cancellationToken);
        await InsertParticipantRecordAsync(
            row,
            profile,
            tenantId,
            userId,
            reviewer.Value,
            legalName,
            tradingName,
            website,
            now,
            cancellationToken);
        await RestoreReviewerAsync(
            reviewer, platformTenantId, cancellationToken);

        return new PublicIntakeProvisioningResult(tenantId, userId);
    }

    private Task<int> InsertTenantAsync(
        Guid tenantId,
        ProvisioningProfile profile,
        string legalName,
        string tradingName,
        string? vatNumber,
        string slug,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var vatStatus = vatNumber is null
            ? MasterDataCodes.VatStatuses.NotApplicable
            : MasterDataCodes.VatStatuses.Registered;
        return store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.tenants (
                id, type_code, legal_name, trading_name, slug,
                status_code, timezone, currency_code, vat_status_code,
                vat_number, settings_json, version, created_at_utc, updated_at_utc)
            VALUES (
                {tenantId}, {profile.TenantType}, {legalName}, {tradingName}, {slug},
                {MasterDataCodes.LifecycleStatuses.Active}, {"Africa/Johannesburg"},
                {MasterDataCodes.Currencies.Zar}, {vatStatus}, {vatNumber},
                CAST({EmptyJson} AS jsonb), 1, {now}, {now})
            """, cancellationToken);
    }

    private Task<int> InsertMembershipAsync(
        Guid tenantId,
        Guid userId,
        Guid reviewerId,
        string role,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.memberships (
                id, tenant_id, user_id, role_code, status_code,
                invited_by, invited_at_utc, accepted_at_utc,
                version, created_at_utc, updated_at_utc)
            VALUES (
                {Guid.NewGuid()}, {tenantId}, {userId}, {role},
                {MasterDataCodes.LifecycleStatuses.Active}, {reviewerId},
                {now}, {now}, 1, {now}, {now})
            """, cancellationToken);

    private async Task InsertParticipantRecordAsync(
        PublicIntakeRow row,
        ProvisioningProfile profile,
        Guid tenantId,
        Guid userId,
        Guid reviewerId,
        string legalName,
        string tradingName,
        string? website,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (row.TypeCode == MasterDataCodes.PublicIntakeTypes.Agency)
        {
            _ = await InsertAgencyAsync(
                row.Id, tenantId, legalName, tradingName, website, now,
                cancellationToken);
            return;
        }
        if (row.TypeCode == MasterDataCodes.PublicIntakeTypes.Advertiser)
        {
            _ = await InsertAdvertiserAsync(
                row.Id, tenantId, legalName, tradingName, website, now,
                cancellationToken);
            return;
        }
        if (profile.SupplierScoped)
        {
            await InsertSupplierAsync(
                row.Id,
                tenantId,
                userId,
                reviewerId,
                profile.Role,
                tradingName,
                now,
                cancellationToken);
        }
    }

    private Task<int> InsertAgencyAsync(
        Guid requestId,
        Guid tenantId,
        string legalName,
        string tradingName,
        string? website,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.agencies (
                id, tenant_id, external_reference, legal_name, trading_name,
                website, status_code, version, created_at_utc, updated_at_utc)
            VALUES (
                {Guid.NewGuid()}, {tenantId}, {requestId.ToString()},
                {legalName}, {tradingName}, {website},
                {MasterDataCodes.LifecycleStatuses.Active}, 1, {now}, {now})
            """, cancellationToken);

    private Task<int> InsertAdvertiserAsync(
        Guid requestId,
        Guid tenantId,
        string legalName,
        string tradingName,
        string? website,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.client_accounts (
                id, tenant_id, external_reference, legal_name, trading_name,
                website, industry, billing_profile_json, status_code,
                version, created_at_utc, updated_at_utc)
            VALUES (
                {Guid.NewGuid()}, {tenantId}, {requestId.ToString()},
                {legalName}, {tradingName}, {website}, NULL, CAST({EmptyJson} AS jsonb),
                {MasterDataCodes.LifecycleStatuses.Active}, 1, {now}, {now})
            """, cancellationToken);

    private async Task InsertSupplierAsync(
        Guid requestId,
        Guid tenantId,
        Guid userId,
        Guid reviewerId,
        string role,
        string tradingName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var supplierId = Guid.NewGuid();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_suppliers (
                id, tenant_id, name, external_reference, version,
                created_at_utc, updated_at_utc, identity_key,
                claim_status_code, claimed_by, claimed_at_utc)
            VALUES (
                {supplierId}, {tenantId}, {tradingName}, {requestId.ToString()}, 1,
                {now}, {now}, {BuildIdentityKey(tradingName, requestId)},
                {MasterDataCodes.SupplierClaimStatuses.Claimed}, {userId}, {now})
            """, cancellationToken);
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_supplier_memberships (
                id, tenant_id, supplier_id, user_id, role_code,
                status_code, invitation_id, created_by, accepted_at_utc,
                version, created_at_utc, updated_at_utc)
            VALUES (
                {Guid.NewGuid()}, {tenantId}, {supplierId}, {userId}, {role},
                {MasterDataCodes.LifecycleStatuses.Active}, NULL, {reviewerId},
                {now}, 1, {now}, {now})
            """, cancellationToken);
    }

    private Task RestoreReviewerAsync(
        ActorId reviewer,
        TenantId platformTenantId,
        CancellationToken cancellationToken) =>
        ApplicationDatabaseSession.SetAsync(
            store.DbContext,
            new UserId(reviewer.Value),
            platformTenantId,
            cancellationToken);

    private static string BuildSlug(string tradingName, Guid requestId)
    {
        var normalized = new string(tradingName.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }
        normalized = normalized.Trim('-');
        if (normalized.Length == 0) normalized = "workspace";
        if (normalized.Length > 60) normalized = normalized[..60].TrimEnd('-');
        return $"{normalized}-{requestId:N}";
    }

    private static string BuildIdentityKey(string tradingName, Guid requestId)
    {
        var value = new string(tradingName.ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .Take(260)
            .ToArray());
        return value.Length == 0 ? requestId.ToString("N") : value;
    }
}

internal sealed record PublicIntakeProvisioningResult(Guid TenantId, Guid UserId);
