using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Identity;

public sealed class DatabaseTenantMembershipSource(GovernanceDbContext dbContext)
    : ITenantMembershipSource
{
    public async Task<TenantMembership?> FindAsync(
        ActorId actorId,
        TenantId requestedTenantId,
        CancellationToken cancellationToken)
    {
        var userId = new UserId(actorId.Value);
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext,
            userId,
            requestedTenantId,
            cancellationToken);

        var role = await FindActiveRoleAsync(userId, requestedTenantId, cancellationToken);
        if (role is null)
        {
            return null;
        }

        var permissions = await FindPermissionsAsync(role.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new TenantMembership(
            requestedTenantId,
            actorId,
            true,
            permissions);
    }

    private async Task<RoleCode?> FindActiveRoleAsync(
        UserId userId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var membershipRole = await dbContext.Memberships
            .Where(item => item.UserId == userId
                && item.TenantId == tenantId
                && item.Status == MasterDataReferences.LifecycleStatuses.Active)
            .Select(item => (string?)item.Role.Value)
            .SingleOrDefaultAsync(cancellationToken);
        if (membershipRole is null ||
            !await ActiveIdentityAndTenantExistAsync(userId, tenantId, cancellationToken))
        {
            return null;
        }

        return new RoleCode(membershipRole);
    }

    private async Task<bool> ActiveIdentityAndTenantExistAsync(
        UserId userId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var userActive = await dbContext.Users.AnyAsync(
            item => item.Id == userId && item.Status == MasterDataReferences.LifecycleStatuses.Active,
            cancellationToken);
        var tenantActive = await dbContext.Tenants.AnyAsync(
            item => item.Id == tenantId && item.Status == MasterDataReferences.LifecycleStatuses.Active,
            cancellationToken);
        return userActive && tenantActive;
    }

    private async Task<IReadOnlySet<PermissionCode>> FindPermissionsAsync(
        RoleCode role,
        CancellationToken cancellationToken)
    {
        var registered = await dbContext.MasterDataItems
            .AsNoTracking()
            .Where(item => item.CollectionCode == MasterDataCodes.Permissions.Collection
                && item.IsActive)
            .Select(item => new { item.Code, item.MetadataJson })
            .ToListAsync(cancellationToken);

        return registered
            .Where(item => PermissionRoleMetadata.ReadRoles(item.MetadataJson)
                .Contains(role.Value))
            .Select(item => new PermissionCode(item.Code))
            .ToHashSet();
    }
}
