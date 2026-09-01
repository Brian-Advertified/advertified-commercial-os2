using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.CommercialSettings;

internal static class CommercialApprovalPolicy
{
    internal static async Task EnsureSelfApprovalAllowedAsync(
        GovernanceDbContext database,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        if (!await AllowsSelfApprovalAsync(database, tenantId, cancellationToken))
        {
            throw new Advertified.Commercial.Application.Opportunity.ApprovalRequiredException();
        }
    }

    internal static Task<bool> AllowsSelfApprovalAsync(
        GovernanceDbContext database,
        TenantId tenantId,
        CancellationToken cancellationToken) =>
        database.Database.SqlQuery<bool>($"""
            SELECT COALESCE((
                SELECT version.allow_self_approval
                FROM commercial.commercial_policies policy
                JOIN commercial.commercial_policy_versions version
                  ON version.tenant_id = policy.tenant_id
                 AND version.id = policy.current_version_id
                WHERE policy.tenant_id = {tenantId.Value}
            ), FALSE) AS "Value"
            """).SingleAsync(cancellationToken);
}
