using System.Security.Cryptography;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands
{
    private async Task<CommandOutcome> UploadAssetOutcomeAsync(
        Guid productId,
        CommandEnvelope<UploadInventoryAssetCommand> envelope,
        CancellationToken cancellationToken)
    {
        var command = envelope.Command;
        var product = await LoadAssetProductAsync(productId, envelope, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory product access denied.");
        if (product.Version != envelope.ExpectedVersion ||
            product.CurrentVersionId != command.ProductVersionId)
        {
            throw new VersionConflictException();
        }
        var assetType = command.AssetType?.Trim().ToUpperInvariant();
        if (assetType is null || !AssetUploadTypes.Contains(assetType))
            throw new ArgumentException("Select a supported inventory image type.");
        var fileName = Path.GetFileName(OpportunityCommandSupport.Required(
            command.Source.FileName, 500, nameof(command.Source.FileName)));
        var detected = InventoryDocumentClassifier.Detect(
            fileName, command.Source.DeclaredMediaType, command.Source.Content,
            maximumSourceBytes);
        if (detected.Code is not (MasterDataCodes.DocumentClasses.Png or
                MasterDataCodes.DocumentClasses.Jpeg))
        {
            throw new ArgumentException("Inventory visual assets must be PNG or JPEG images.");
        }
        var scan = await store.MalwareScanner.ScanAsync(command.Source.Content, cancellationToken);
        if (!scan.IsClean) throw new UnsafeInventorySourceException();
        var now = timeProvider.GetUtcNow();
        var importId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var hash = Convert.ToHexStringLower(SHA256.HashData(command.Source.Content));
        var quarantineKey = $"quarantine/{envelope.TenantId.Value:N}/{importId:N}/{hash}";
        var objectKey = $"protected/{envelope.TenantId.Value:N}/assets/{assetId:N}/{hash}";
        await store.ObjectStore.PutAsync(
            quarantineKey, command.Source.Content, detected.MediaType, cancellationToken);
        await store.ObjectStore.PutAsync(
            objectKey, command.Source.Content, detected.MediaType, cancellationToken);
        await PersistAssetAsync(
            productId, product, envelope, assetType, fileName, detected, importId, assetId,
            quarantineKey, objectKey, hash, now, cancellationToken);
        var view = new InventoryAssetView(
            assetType!, detected.MediaType, hash, $"inventory-import:{importId}", assetId,
            MasterDataCodes.AssetRightsStatuses.Unknown, null, null, false, 1, [], "ZA");
        return OpportunityCommandSupport.Outcome(
            envelope, view, productId, product.Version + 1,
            MasterDataReferences.CommercialResourceTypes.InventoryProduct,
            MasterDataReferences.CommercialActions.InventoryAssetUploaded,
            MasterDataReferences.CommercialEventTypes.InventoryAssetUploaded, now);
    }

    private Task<AssetProductRow?> LoadAssetProductAsync(
        Guid productId,
        CommandEnvelope<UploadInventoryAssetCommand> envelope,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<AssetProductRow>($"""
            SELECT id AS "Id", supplier_id AS "SupplierId",
                current_version_id AS "CurrentVersionId", version AS "Version"
            FROM commercial.inventory_products
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {productId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Active}
            FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);

    private async Task PersistAssetAsync(
        Guid productId,
        AssetProductRow product,
        CommandEnvelope<UploadInventoryAssetCommand> envelope,
        string assetType,
        string fileName,
        InventoryDocumentClass detected,
        Guid importId,
        Guid assetId,
        string quarantineKey,
        string objectKey,
        string hash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_imports (
                id, tenant_id, supplier_id, source_file_name, declared_media_type,
                document_class_collection_code, document_class_code,
                status_code, scan_status_code,
                quarantine_object_key, protected_object_key, source_hash, source_size,
                created_by, version, created_at_utc, updated_at_utc)
            VALUES ({importId}, {envelope.TenantId.Value}, {product.SupplierId}, {fileName},
                {detected.MediaType}, {MasterDataCodes.DocumentClasses.Collection},
                {detected.Code},
                {MasterDataCodes.LifecycleStatuses.Completed},
                {MasterDataCodes.MalwareScanStatuses.Clean}, {quarantineKey}, {objectKey},
                {hash}, {envelope.Command.Source.Content.LongLength},
                {envelope.ActorId.Value}, 1, {now}, {now});
            INSERT INTO commercial.inventory_assets (
                id, tenant_id, product_version_id, asset_type_code, object_key,
                content_hash, media_type, source_import_id)
            VALUES ({assetId}, {envelope.TenantId.Value}, {product.CurrentVersionId},
                {assetType}, {objectKey}, {hash},
                {detected.MediaType}, {importId});
            """, cancellationToken);
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_products
            SET version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {productId}
              AND version = {envelope.ExpectedVersion};
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        await RecordStepAsync(
            envelope.TenantId, importId,
            MasterDataCodes.InventoryImportStepTypes.UploadProtection,
            MasterDataCodes.LifecycleStatuses.Completed, now, cancellationToken);
        await RecordStepAsync(
            envelope.TenantId, importId,
            MasterDataCodes.InventoryImportStepTypes.Classification,
            MasterDataCodes.LifecycleStatuses.Completed, now, cancellationToken);
    }

    private static readonly HashSet<string> AssetUploadTypes = new(StringComparer.Ordinal)
    {
        MasterDataCodes.AssetTypes.Logo,
        MasterDataCodes.AssetTypes.ProductImage,
        MasterDataCodes.AssetTypes.OohPhoto,
    };

    private sealed record AssetProductRow(
        Guid Id, Guid SupplierId, Guid CurrentVersionId, long Version);
}
