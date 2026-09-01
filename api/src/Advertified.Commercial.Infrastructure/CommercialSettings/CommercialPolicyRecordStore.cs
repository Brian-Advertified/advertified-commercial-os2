using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.CommercialSettings;

public sealed class CommercialPolicyRecordStore(GovernanceDbContext dbContext)
{
    internal GovernanceDbContext DbContext => dbContext;

    internal async Task<IDbContextTransaction> BeginSessionAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId.Value), tenantId, cancellationToken);
        return transaction;
    }

    internal Task<int> LockAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var lockKey = $"commercial-policy:{tenantId.Value:N}";
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
    }

    internal Task<CommercialPolicyRow?> FindCurrentAsync(
        TenantId tenantId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<CommercialPolicyRow>($"""
            SELECT version.id AS "Id", policy.id AS "PolicyId",
                policy.tenant_id AS "TenantId", version.version_number AS "VersionNumber",
                version.markup_basis_points AS "MarkupBasisPoints",
                version.management_fee_basis_points AS "ManagementFeeBasisPoints",
                version.commission_basis_points AS "CommissionBasisPoints",
                version.vat_status_code AS "VatStatus",
                version.vat_rate_basis_points AS "VatRateBasisPoints",
                version.prices_include_vat AS "PricesIncludeVat",
                version.currency_code AS "Currency",
                version.booking_approval_threshold_minor AS "BookingApprovalThresholdMinor",
                version.allow_self_approval AS "AllowSelfApproval",
                version.created_by AS "CreatedBy", version.created_at_utc AS "CreatedAtUtc",
                policy.version AS "Version"
            FROM commercial.commercial_policies policy
            JOIN commercial.commercial_policy_versions version
              ON version.tenant_id = policy.tenant_id
             AND version.id = policy.current_version_id
            WHERE policy.tenant_id = {tenantId.Value}
            """).SingleOrDefaultAsync(cancellationToken);

    internal async Task InsertFirstAsync(
        TenantId tenantId,
        Guid policyId,
        Guid versionId,
        ActorId actorId,
        ValidatedCommercialPolicy policy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.commercial_policies (
                id, tenant_id, version, created_at_utc, updated_at_utc)
            VALUES ({policyId}, {tenantId.Value}, 1, {now}, {now})
            """, cancellationToken);
        await InsertVersionAsync(
            tenantId, policyId, versionId, 1, actorId, policy, now, cancellationToken);
        var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.commercial_policies
            SET current_version_id = {versionId}
            WHERE tenant_id = {tenantId.Value} AND id = {policyId} AND version = 1
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
    }

    internal async Task InsertNextAsync(
        TenantId tenantId,
        CommercialPolicyRow current,
        Guid versionId,
        ActorId actorId,
        ValidatedCommercialPolicy policy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await InsertVersionAsync(
            tenantId, current.PolicyId, versionId, current.VersionNumber + 1,
            actorId, policy, now, cancellationToken);
        var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.commercial_policies
            SET current_version_id = {versionId}, version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {tenantId.Value} AND id = {current.PolicyId}
              AND version = {current.Version}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
    }

    private Task<int> InsertVersionAsync(
        TenantId tenantId,
        Guid policyId,
        Guid versionId,
        int versionNumber,
        ActorId actorId,
        ValidatedCommercialPolicy policy,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.commercial_policy_versions (
                id, tenant_id, policy_id, version_number, markup_basis_points,
                management_fee_basis_points, commission_basis_points,
                vat_status_code, vat_rate_basis_points, prices_include_vat,
                currency_code, booking_approval_threshold_minor, allow_self_approval,
                created_by, created_at_utc)
            VALUES ({versionId}, {tenantId.Value}, {policyId}, {versionNumber},
                {policy.MarkupBasisPoints}, {policy.ManagementFeeBasisPoints},
                {policy.CommissionBasisPoints}, {policy.VatStatus},
                {policy.VatRateBasisPoints}, {policy.PricesIncludeVat}, {policy.Currency},
                {policy.BookingApprovalThresholdMinor}, {policy.AllowSelfApproval},
                {actorId.Value}, {now})
            """, cancellationToken);
}
