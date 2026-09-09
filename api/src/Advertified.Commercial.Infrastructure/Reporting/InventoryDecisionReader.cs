using System.Globalization;
using System.Text;
using Advertified.Commercial.Application.Reporting;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Advertified.Commercial.Infrastructure.Reporting;

public sealed class InventoryDecisionReader(
    GovernanceDbContext dbContext,
    ITenantAuthorizer authorizer,
    InventorySupplierAccessPolicy supplierAccess) : IInventoryDecisionReader
{
    private const int PageSize = 100;

    public async Task<InventoryDecisionReportView> ReadAsync(
        ActorId actorId, TenantId tenantId, Guid? briefVersionId,
        Guid? inventoryProductId, string? cursor,
        CancellationToken cancellationToken)
    {
        if (briefVersionId.HasValue == inventoryProductId.HasValue)
            throw new ArgumentException("Choose one campaign or inventory product report.");
        var before = InventoryDecisionCursor.Decode(cursor);
        var permission = inventoryProductId.HasValue
            ? MasterDataReferences.Permissions.InventoryView
            : MasterDataReferences.Permissions.PlanView;
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, permission, cancellationToken);
        if (!decision.IsAllowed) throw new UnauthorizedAccessException("Report access denied.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId.Value), tenantId, cancellationToken);
        if (inventoryProductId.HasValue)
            await supplierAccess.EnsureProductAccessAsync(
                actorId, tenantId, inventoryProductId.Value, cancellationToken);
        var rows = await ReadRowsAsync(
            briefVersionId, inventoryProductId, before, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var page = rows.Take(PageSize).ToArray();
        var hasMore = rows.Count > PageSize;
        var next = hasMore && page.Length > 0
            ? InventoryDecisionCursor.Encode(page[^1]) : null;
        return new(page, hasMore, inventoryProductId.HasValue, next);
    }

    private async Task<List<InventoryDecisionView>> ReadRowsAsync(
        Guid? briefVersionId, Guid? inventoryProductId,
        InventoryDecisionCursor? before, CancellationToken cancellationToken)
    {
        DateTimeOffset? beforeTime = before?.DecidedAtUtc;
        Guid? beforeEvent = before?.EventId;
        Guid? beforeProduct = before?.ProductId;
        try
        {
            return await dbContext.Database.SqlQuery<InventoryDecisionView>($"""
                SELECT * FROM commercial.read_inventory_decisions(
                    {briefVersionId}::uuid, {inventoryProductId}::uuid,
                    {beforeTime}::timestamptz, {beforeEvent}::uuid,
                    {beforeProduct}::uuid)
                """).ToListAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.InsufficientPrivilege)
        {
            throw new UnauthorizedAccessException("Report access denied.", exception);
        }
    }
}

internal sealed record InventoryDecisionCursor(
    DateTimeOffset DecidedAtUtc, Guid EventId, Guid ProductId)
{
    internal static string Encode(InventoryDecisionView item)
    {
        var raw = string.Join('|', item.DecidedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            item.EventId.ToString("N"), item.ProductId.ToString("N"));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    internal static InventoryDecisionCursor? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var value = cursor.Replace('-', '+').Replace('_', '/');
            value = value.PadRight(value.Length + ((4 - value.Length % 4) % 4), '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(value)).Split('|');
            if (parts.Length != 3 ||
                !DateTimeOffset.TryParseExact(parts[0], "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var decidedAt) ||
                !Guid.TryParseExact(parts[1], "N", out var eventId) ||
                !Guid.TryParseExact(parts[2], "N", out var productId))
                throw new FormatException();
            return new(decidedAt, eventId, productId);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Inventory decision cursor is invalid.", nameof(cursor), exception);
        }
    }
}
