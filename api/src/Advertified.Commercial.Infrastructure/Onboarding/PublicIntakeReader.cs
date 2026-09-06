using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Onboarding;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Onboarding;

public sealed class PublicIntakeReader(
    PublicIntakeStore store,
    ITenantAuthorizer authorizer) : IPublicIntakeReader
{
    public async Task<CursorPage<PublicIntakeView>> ListAsync(
        ActorId actorId,
        TenantId platformTenantId,
        string? status,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId,
            platformTenantId,
            MasterDataReferences.Permissions.PublicIntakeView,
            cancellationToken);
        if (!decision.IsAllowed)
            throw new UnauthorizedAccessException("Public intake access denied.");
        await using var transaction = await store.DbContext.Database.BeginTransactionAsync(
            cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            store.DbContext, new UserId(actorId.Value), platformTenantId, cancellationToken);
        var isPlatform = await store.DbContext.Tenants.AnyAsync(
            tenant => tenant.Id == platformTenantId &&
                tenant.Type == new TenantTypeCode(MasterDataCodes.TenantTypes.Platform),
            cancellationToken);
        if (!isPlatform)
            throw new UnauthorizedAccessException("Public intake access denied.");
        var (effectiveLimit, offset) = PublicIntakeStore.ParsePage(limit, cursor);
        var rows = await store.ListAsync(status, effectiveLimit, offset, cancellationToken);
        var page = PublicIntakeStore.Page(
            rows.Select(row => row.ToView()).ToArray(), effectiveLimit, offset);
        await transaction.CommitAsync(cancellationToken);
        return page;
    }
}
