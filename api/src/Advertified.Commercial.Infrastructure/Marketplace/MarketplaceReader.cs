using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Advertified.Commercial.Application.Marketplace;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Marketplace;

public sealed class MarketplaceReader(
    MarketplaceRecordStore store,
    ITenantAuthorizer authorizer,
    InventorySupplierAccessPolicy supplierAccess,
    TimeProvider timeProvider) : IMarketplaceReader
{
    public async Task<MarketplaceListingPage> SearchListingsAsync(
        ActorId actorId, TenantId tenantId, MarketplaceSearchQuery query,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId,
            MasterDataReferences.Permissions.MarketplaceView, cancellationToken);
        var filters = MarketplacePolicy.ValidateSearch(query);
        var pageSize = MarketplacePolicy.ValidatePageSize(query.PageSize);
        var cursor = MarketplaceCursor.Decode(query.Cursor);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var supplierScope = await supplierAccess.ResolveSupplierScopeAsync(actorId, tenantId, cancellationToken);
        var rows = await SearchListingRowsAsync(
            filters, cursor, pageSize + 1, supplierScope, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var page = rows.Take(pageSize).ToArray();
        var next = rows.Count > pageSize
            ? MarketplaceCursor.Encode(page[^1].UpdatedAtUtc, page[^1].Id) : null;
        return new MarketplaceListingPage(page.Select(item => item.ToView()).ToArray(), next);
    }

    public async Task<MarketplaceListingView> GetListingAsync(
        ActorId actorId, TenantId tenantId, Guid listingId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId,
            MasterDataReferences.Permissions.MarketplaceView, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var row = await store.FindListingAsync(listingId, false, cancellationToken)
            ?? throw new UnauthorizedAccessException("Marketplace listing access denied.");
        var scope = await supplierAccess.ResolveSupplierScopeAsync(actorId, tenantId, cancellationToken);
        if (scope is not null)
        {
            if (row.SupplierTenantId != tenantId.Value)
                throw new UnauthorizedAccessException("Marketplace listing access denied.");
            await supplierAccess.EnsureProductAccessAsync(actorId, tenantId, row.ProductId, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return row.ToView();
    }

    public async Task<MarketplaceRfqPage> ListRfqsAsync(
        ActorId actorId, TenantId tenantId, MarketplaceRfqQuery query,
        CancellationToken cancellationToken)
    {
        await EnsureRfqReadAllowedAsync(actorId, tenantId, cancellationToken);
        var pageSize = MarketplacePolicy.ValidatePageSize(query.PageSize);
        var status = MarketplacePolicy.ValidateRfqStatus(query.Status);
        var cursor = MarketplaceCursor.Decode(query.Cursor);
        var now = timeProvider.GetUtcNow();
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var rows = await ListRfqRowsAsync(
            now, status, cursor, pageSize + 1, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var page = rows.Take(pageSize).ToArray();
        var next = rows.Count > pageSize
            ? MarketplaceCursor.Encode(page[^1].UpdatedAtUtc, page[^1].Id) : null;
        return new MarketplaceRfqPage(page.Select(item => item.ToView()).ToArray(), next);
    }

    public async Task<MarketplaceRfqView> GetRfqAsync(
        ActorId actorId, TenantId tenantId, Guid rfqId,
        CancellationToken cancellationToken)
    {
        await EnsureRfqReadAllowedAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var row = await store.FindRfqAsync(
            rfqId, timeProvider.GetUtcNow(), cancellationToken)
            ?? throw new UnauthorizedAccessException("Marketplace request access denied.");
        await transaction.CommitAsync(cancellationToken);
        return row.ToView();
    }

    private Task<List<MarketplaceListingRow>> SearchListingRowsAsync(
        MarketplaceSearchFilters filters, MarketplaceCursorValue? cursor, int take,
        Guid[]? supplierScope,
        CancellationToken cancellationToken)
    {
        var suffix = cursor is null
            ? """
                WHERE listing.status_code = {3}
                  AND ({5}::uuid[] IS NULL OR version.supplier_id = ANY({5}))
                  AND ({0}::text IS NULL OR version.product_name ILIKE '%' || {0} || '%'
                       OR version.supplier_name ILIKE '%' || {0} || '%')
                  AND ({1}::text IS NULL OR version.channel_code = {1})
                  AND ({2}::text IS NULL OR version.geography ILIKE '%' || {2} || '%')
                ORDER BY listing.updated_at_utc DESC, listing.id DESC LIMIT {4}
                """
            : """
                WHERE listing.status_code = {3}
                  AND ({7}::uuid[] IS NULL OR version.supplier_id = ANY({7}))
                  AND ({0}::text IS NULL OR version.product_name ILIKE '%' || {0} || '%'
                       OR version.supplier_name ILIKE '%' || {0} || '%')
                  AND ({1}::text IS NULL OR version.channel_code = {1})
                  AND ({2}::text IS NULL OR version.geography ILIKE '%' || {2} || '%')
                  AND (listing.updated_at_utc, listing.id) < ({4}, {5})
                ORDER BY listing.updated_at_utc DESC, listing.id DESC LIMIT {6}
                """;
        var args = cursor is null
            ? new object?[] { filters.Search, filters.Channel, filters.Geography,
                MasterDataCodes.MarketplaceListingStatuses.Published, take, supplierScope }
            : [filters.Search, filters.Channel, filters.Geography,
                MasterDataCodes.MarketplaceListingStatuses.Published,
                cursor.UpdatedAtUtc, cursor.Id, take, supplierScope];
        return store.DbContext.Database.SqlQuery<MarketplaceListingRow>(
            FormattableStringFactory.Create(
                MarketplaceRecordStore.ListingSelect + Environment.NewLine + suffix, args))
            .ToListAsync(cancellationToken);
    }

    private Task<List<MarketplaceRfqRow>> ListRfqRowsAsync(
        DateTimeOffset now, string? status, MarketplaceCursorValue? cursor, int take,
        CancellationToken cancellationToken)
    {
        var projection = "SELECT * FROM (" + MarketplaceRecordStore.RfqSelect + ") projected";
        var suffix = cursor is null
            ? " WHERE ({6}::text IS NULL OR projected.\"Status\" = {6}) " +
              "ORDER BY projected.\"UpdatedAtUtc\" DESC, projected.\"Id\" DESC LIMIT {7}"
            : " WHERE ({6}::text IS NULL OR projected.\"Status\" = {6}) " +
              "AND (projected.\"UpdatedAtUtc\", projected.\"Id\") < ({7}, {8}) " +
              "ORDER BY projected.\"UpdatedAtUtc\" DESC, projected.\"Id\" DESC LIMIT {9}";
        var args = cursor is null
            ? MarketplaceRecordStore.RfqParameters(now, status, take)
            : MarketplaceRecordStore.RfqParameters(
                now, status, cursor.UpdatedAtUtc, cursor.Id, take);
        return store.DbContext.Database.SqlQuery<MarketplaceRfqRow>(
            FormattableStringFactory.Create(projection + suffix, args))
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureRfqReadAllowedAsync(
        ActorId actorId, TenantId tenantId, CancellationToken cancellationToken)
    {
        var buyer = await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.RfqReview, cancellationToken);
        if (buyer.IsAllowed) return;
        await EnsureAllowedAsync(actorId, tenantId,
            MasterDataReferences.Permissions.RfqRespond, cancellationToken);
    }

    private async Task EnsureAllowedAsync(
        ActorId actorId, TenantId tenantId, PermissionCode permission,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, permission, cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Marketplace access denied.");
        }
    }

}

internal sealed record MarketplaceCursorValue(DateTimeOffset UpdatedAtUtc, Guid Id);

internal static class MarketplaceCursor
{
    private const int MaximumEncodedLength = 256;

    internal static string Encode(DateTimeOffset updatedAtUtc, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{updatedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture)}|{id:D}"));

    internal static MarketplaceCursorValue? Decode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Length > MaximumEncodedLength) throw InvalidCursor();
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(value)).Split('|');
            if (parts.Length != 2 ||
                !long.TryParse(parts[0], CultureInfo.InvariantCulture, out var ticks) ||
                !Guid.TryParse(parts[1], out var id)) throw new FormatException();
            return new MarketplaceCursorValue(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            throw InvalidCursor(exception);
        }
    }

    private static ArgumentException InvalidCursor(Exception? inner = null) =>
        new("The marketplace cursor is invalid.", inner);
}
