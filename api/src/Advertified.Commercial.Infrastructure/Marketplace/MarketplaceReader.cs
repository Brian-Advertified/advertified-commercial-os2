using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Advertified.Commercial.Application.Marketplace;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Marketplace;

public sealed class MarketplaceReader(
    MarketplaceRecordStore store,
    ITenantAuthorizer authorizer,
    TimeProvider timeProvider) : IMarketplaceReader
{
    public async Task<MarketplaceListingPage> SearchListingsAsync(
        ActorId actorId, TenantId tenantId, MarketplaceSearchQuery query,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId,
            MasterDataReferences.Permissions.MarketplaceView, cancellationToken);
        var pageSize = ValidatePageSize(query.PageSize);
        var cursor = MarketplaceCursor.Decode(query.Cursor);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var rows = await SearchListingRowsAsync(
            query, cursor, pageSize + 1, cancellationToken);
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
        var row = await store.FindListingAsync(listingId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Marketplace listing access denied.");
        await transaction.CommitAsync(cancellationToken);
        return row.ToView();
    }

    public async Task<MarketplaceRfqPage> ListRfqsAsync(
        ActorId actorId, TenantId tenantId, MarketplaceRfqQuery query,
        CancellationToken cancellationToken)
    {
        await EnsureRfqReadAllowedAsync(actorId, tenantId, cancellationToken);
        var pageSize = ValidatePageSize(query.PageSize);
        var cursor = MarketplaceCursor.Decode(query.Cursor);
        var now = timeProvider.GetUtcNow();
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var rows = await ListRfqRowsAsync(now, cursor, pageSize + 1, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var filtered = string.IsNullOrWhiteSpace(query.Status)
            ? rows : rows.Where(item => string.Equals(
                item.Status, query.Status.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        var page = filtered.Take(pageSize).ToArray();
        var next = rows.Count > pageSize && page.Length > 0
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
        MarketplaceSearchQuery query, MarketplaceCursorValue? cursor, int take,
        CancellationToken cancellationToken)
    {
        var search = Normalize(query.Search);
        var channel = Normalize(query.Channel)?.ToUpperInvariant();
        var geography = Normalize(query.Geography);
        var suffix = cursor is null
            ? """
                WHERE listing.status_code = {3}
                  AND ({0}::text IS NULL OR version.product_name ILIKE '%' || {0} || '%'
                       OR version.supplier_name ILIKE '%' || {0} || '%')
                  AND ({1}::text IS NULL OR version.channel_code = {1})
                  AND ({2}::text IS NULL OR version.geography ILIKE '%' || {2} || '%')
                ORDER BY listing.updated_at_utc DESC, listing.id DESC LIMIT {4}
                """
            : """
                WHERE listing.status_code = {3}
                  AND ({0}::text IS NULL OR version.product_name ILIKE '%' || {0} || '%'
                       OR version.supplier_name ILIKE '%' || {0} || '%')
                  AND ({1}::text IS NULL OR version.channel_code = {1})
                  AND ({2}::text IS NULL OR version.geography ILIKE '%' || {2} || '%')
                  AND (listing.updated_at_utc, listing.id) < ({4}, {5})
                ORDER BY listing.updated_at_utc DESC, listing.id DESC LIMIT {6}
                """;
        var args = cursor is null
            ? new object?[] { search, channel, geography,
                MasterDataCodes.MarketplaceListingStatuses.Published, take }
            : [search, channel, geography,
                MasterDataCodes.MarketplaceListingStatuses.Published,
                cursor.UpdatedAtUtc, cursor.Id, take];
        return store.DbContext.Database.SqlQuery<MarketplaceListingRow>(
            FormattableStringFactory.Create(
                MarketplaceRecordStore.ListingSelect + Environment.NewLine + suffix, args))
            .ToListAsync(cancellationToken);
    }

    private Task<List<MarketplaceRfqRow>> ListRfqRowsAsync(
        DateTimeOffset now, MarketplaceCursorValue? cursor, int take,
        CancellationToken cancellationToken)
    {
        var suffix = cursor is null
            ? " ORDER BY rfq.updated_at_utc DESC, rfq.id DESC LIMIT {6}"
            : " WHERE (rfq.updated_at_utc, rfq.id) < ({6}, {7}) " +
              "ORDER BY rfq.updated_at_utc DESC, rfq.id DESC LIMIT {8}";
        var args = cursor is null
            ? MarketplaceRecordStore.RfqParameters(now, take)
            : MarketplaceRecordStore.RfqParameters(
                now, cursor.UpdatedAtUtc, cursor.Id, take);
        return store.DbContext.Database.SqlQuery<MarketplaceRfqRow>(
            FormattableStringFactory.Create(MarketplaceRecordStore.RfqSelect + suffix, args))
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

    private static int ValidatePageSize(int value) => value is >= 1 and <= 100
        ? value : throw new ArgumentOutOfRangeException(nameof(value));

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed record MarketplaceCursorValue(DateTimeOffset UpdatedAtUtc, Guid Id);

internal static class MarketplaceCursor
{
    internal static string Encode(DateTimeOffset updatedAtUtc, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{updatedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture)}|{id:D}"));

    internal static MarketplaceCursorValue? Decode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(value)).Split('|');
            if (parts.Length != 2 ||
                !long.TryParse(parts[0], CultureInfo.InvariantCulture, out var ticks) ||
                !Guid.TryParse(parts[1], out var id)) throw new FormatException();
            return new MarketplaceCursorValue(
                new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch (FormatException)
        {
            throw new ArgumentException("The marketplace cursor is invalid.", nameof(value));
        }
    }
}
