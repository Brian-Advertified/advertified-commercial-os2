using System.Runtime.CompilerServices;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryReader
{
    public async Task<IReadOnlyList<InventorySemanticRecallView>> GetSemanticRecallAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid productId,
        int limit,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(
            actorId, tenantId, MasterDataReferences.Permissions.InventoryView, cancellationToken);
        if (limit is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        await supplierAccess.EnsureProductAccessAsync(
            actorId, tenantId, productId, cancellationToken);
        var supplierScope = await supplierAccess.ResolveSupplierScopeAsync(
            actorId, tenantId, cancellationToken);
        var rows = await store.DbContext.Database.SqlQuery<InventorySemanticRecallRow>($"""
            WITH target AS (
                SELECT embedding.embedding, embedding.provider_code, embedding.model_code,
                    embedding.product_version_id
                FROM commercial.inventory_products product
                JOIN commercial.inventory_product_embeddings embedding
                  ON embedding.tenant_id = product.tenant_id
                 AND embedding.product_version_id = product.current_version_id
                WHERE product.tenant_id = {tenantId.Value} AND product.id = {productId}
                ORDER BY embedding.created_at_utc DESC, embedding.id DESC
                LIMIT 1),
            peer_embeddings AS (
                SELECT DISTINCT ON (embedding.product_version_id) embedding.*
                FROM target
                JOIN commercial.inventory_product_embeddings embedding
                  ON embedding.tenant_id = {tenantId.Value}
                 AND embedding.provider_code = target.provider_code
                 AND embedding.model_code = target.model_code
                 AND embedding.product_version_id <> target.product_version_id
                ORDER BY embedding.product_version_id,
                    embedding.created_at_utc DESC, embedding.id DESC)
            SELECT peer.id AS "ProductId", version.id AS "ProductVersionId",
                version.name AS "Name", version.geography AS "Geography",
                (1 - (embedding.embedding <=> target.embedding))::numeric AS "Similarity"
            FROM target
            JOIN peer_embeddings embedding ON TRUE
            JOIN commercial.inventory_products peer
             ON peer.tenant_id = embedding.tenant_id
             AND peer.current_version_id = embedding.product_version_id
             AND peer.status_code = {MasterDataCodes.LifecycleStatuses.Active}
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = peer.tenant_id AND version.id = peer.current_version_id
            WHERE ({supplierScope}::uuid[] IS NULL OR peer.supplier_id = ANY({supplierScope}))
              AND NOT EXISTS (
                SELECT 1 FROM commercial.inventory_product_identity_links identity_link
                WHERE identity_link.tenant_id = peer.tenant_id
                  AND identity_link.duplicate_product_id = peer.id)
            ORDER BY embedding.embedding <=> target.embedding, peer.id
            LIMIT {limit}
            """).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows.Select(item => item.ToView()).ToArray();
    }

    public async Task<IReadOnlyList<InventoryDuplicateCandidateView>> ListDuplicateCandidatesAsync(
        ActorId actorId,
        TenantId tenantId,
        string? status,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(
            actorId, tenantId, MasterDataReferences.Permissions.InventoryReview, cancellationToken);
        var normalized = string.IsNullOrWhiteSpace(status)
            ? MasterDataCodes.InventoryDuplicateStatuses.Open
            : status.Trim().ToUpperInvariant();
        if (normalized is not (MasterDataCodes.InventoryDuplicateStatuses.Open or
            MasterDataCodes.InventoryDuplicateStatuses.ConfirmedSameIdentity or
            MasterDataCodes.InventoryDuplicateStatuses.Dismissed or
            MasterDataCodes.InventoryDuplicateStatuses.Deferred))
        {
            throw new ArgumentException("Select a supported duplicate candidate status.");
        }
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var rows = await store.DbContext.Database.SqlQuery<InventoryDuplicateCandidateRow>(
            FormattableStringFactory.Create(
                InventoryDuplicateQueries.Select +
                " WHERE candidate.tenant_id = {0} AND candidate.status_code = {1} " +
                "ORDER BY candidate.detected_at_utc DESC, candidate.id",
                tenantId.Value, normalized)).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows.Select(item => item.ToView()).ToArray();
    }

    public async Task<InventoryAssetContent> GetApprovedAssetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(
            actorId, tenantId, MasterDataReferences.Permissions.InventoryView, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var now = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        await supplierAccess.EnsureAssetAccessAsync(actorId, tenantId, assetId, cancellationToken);
        var rows = await store.DbContext.Database.SqlQuery<ApprovedAssetRow>($"""
            SELECT object_key AS "ObjectKey", media_type AS "MediaType",
                content_hash AS "ContentHash"
            FROM commercial.read_approved_inventory_asset({assetId}, {now})
            """).ToListAsync(cancellationToken);
        var row = rows.SingleOrDefault()
            ?? throw new UnauthorizedAccessException("Approved inventory asset access denied.");
        await transaction.CommitAsync(cancellationToken);
        var content = await store.ObjectStore.ReadAsync(row.ObjectKey, cancellationToken);
        return new(content, row.MediaType, row.ContentHash);
    }

    private sealed record ApprovedAssetRow(
        string ObjectKey,
        string MediaType,
        string ContentHash);
}
