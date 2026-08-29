using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Foundation;

public sealed class CommercialFoundationReader(
    GovernanceDbContext dbContext,
    ITenantAuthorizer authorizer) : ICommercialFoundationReader
{
    public async Task<TenantView> GetTenantAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, MasterDataReferences.Permissions.TenantRead, cancellationToken);
        await using var transaction = await BeginSessionAsync(actorId, tenantId, cancellationToken);
        var entity = await dbContext.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == tenantId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Tenant access denied.");
        await transaction.CommitAsync(cancellationToken);
        return FoundationViewMapper.ToView(entity);
    }

    public async Task<CursorPage<ClientAccountView>> ListClientAccountsAsync(
        ActorId actorId,
        TenantId tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var page = CursorPageFactory.Parse(limit, cursor);
        await EnsureAllowedAsync(actorId, tenantId, MasterDataReferences.Permissions.ClientAccountRead, cancellationToken);
        await using var transaction = await BeginSessionAsync(actorId, tenantId, cancellationToken);
        var rows = await dbContext.ClientAccounts.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.TradingName).ThenBy(item => item.Id)
            .Skip(page.Offset).Take(page.Limit + 1)
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CursorPageFactory.Create(
            rows.Select(FoundationViewMapper.ToView).ToArray(),
            page.Limit,
            page.Offset);
    }

    public async Task<CursorPage<MembershipView>> ListMembershipsAsync(
        ActorId actorId,
        TenantId tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var page = CursorPageFactory.Parse(limit, cursor);
        await EnsureAllowedAsync(actorId, tenantId, MasterDataReferences.Permissions.MembershipRead, cancellationToken);
        await using var transaction = await BeginSessionAsync(actorId, tenantId, cancellationToken);
        var rows = await dbContext.Memberships.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.UpdatedAtUtc).ThenBy(item => item.Id)
            .Skip(page.Offset).Take(page.Limit + 1)
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CursorPageFactory.Create(
            rows.Select(FoundationViewMapper.ToView).ToArray(),
            page.Limit,
            page.Offset);
    }

    public async Task<CursorPage<AgencyView>> ListAgenciesAsync(
        ActorId actorId,
        TenantId tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var page = CursorPageFactory.Parse(limit, cursor);
        await EnsureAllowedAsync(actorId, tenantId, MasterDataReferences.Permissions.AgencyRead, cancellationToken);
        await using var transaction = await BeginSessionAsync(actorId, tenantId, cancellationToken);
        var rows = await dbContext.Agencies.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.TradingName).ThenBy(item => item.Id)
            .Skip(page.Offset).Take(page.Limit + 1)
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CursorPageFactory.Create(
            rows.Select(FoundationViewMapper.ToView).ToArray(),
            page.Limit,
            page.Offset);
    }

    public async Task<CursorPage<ContactView>> ListContactsAsync(
        ActorId actorId,
        TenantId tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var page = CursorPageFactory.Parse(limit, cursor);
        await EnsureAllowedAsync(actorId, tenantId, MasterDataReferences.Permissions.ContactRead, cancellationToken);
        await using var transaction = await BeginSessionAsync(actorId, tenantId, cancellationToken);
        var rows = await dbContext.Contacts.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.Name).ThenBy(item => item.Id)
            .Skip(page.Offset).Take(page.Limit + 1)
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CursorPageFactory.Create(
            rows.Select(FoundationViewMapper.ToView).ToArray(),
            page.Limit,
            page.Offset);
    }

    private async Task EnsureAllowedAsync(
        ActorId actorId,
        TenantId tenantId,
        PermissionCode permission,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId,
            tenantId,
            permission,
            cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Tenant access denied.");
        }
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginSessionAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext,
            new UserId(actorId.Value),
            tenantId,
            cancellationToken);
        return transaction;
    }
}
