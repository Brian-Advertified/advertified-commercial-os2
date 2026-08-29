using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Identity;

public sealed class IdentityWorkspaceReader(GovernanceDbContext dbContext)
    : IIdentityWorkspaceReader
{
    public async Task<CurrentUserView> GetCurrentUserAsync(
        UserId userId,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext,
            userId,
            tenantId: null,
            cancellationToken);

        var view = await dbContext.Users
            .Where(item => item.Id == userId && item.Status == MasterDataReferences.LifecycleStatuses.Active)
            .Select(item => new CurrentUserView(
                item.Id.Value,
                item.Email.Value,
                item.DisplayName,
                item.Phone,
                item.MfaEnabled,
                item.Version))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("Identity access denied.");

        await transaction.CommitAsync(cancellationToken);
        return view;
    }

    public async Task<IReadOnlyList<WorkspaceView>> ListWorkspacesAsync(
        UserId userId,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext,
            userId,
            tenantId: null,
            cancellationToken);

        var permissionMetadata = await dbContext.MasterDataItems
            .Where(item => item.CollectionCode == MasterDataCodes.Permissions.Collection
                && item.Code == MasterDataReferences.Permissions.WorkspaceRead.Value
                && item.IsActive)
            .Select(item => item.MetadataJson)
            .SingleOrDefaultAsync(cancellationToken);
        var allowedRoles = permissionMetadata is null
            ? []
            : PermissionRoleMetadata.ReadRoles(permissionMetadata).ToArray();

        var workspaces = await (
                from membership in dbContext.Memberships
                join tenant in dbContext.Tenants on membership.TenantId equals tenant.Id
                where membership.UserId == userId
                    && membership.Status == MasterDataReferences.LifecycleStatuses.Active
                    && tenant.Status == MasterDataReferences.LifecycleStatuses.Active
                orderby tenant.TradingName, tenant.Id
                select new WorkspaceView(
                    membership.Id.Value,
                    tenant.Id.Value,
                    tenant.TradingName,
                    tenant.Slug.Value,
                    membership.Role.Value,
                    membership.Version))
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return workspaces
            .Where(item => allowedRoles.Contains(item.RoleCode, StringComparer.Ordinal))
            .ToArray();
    }

}
