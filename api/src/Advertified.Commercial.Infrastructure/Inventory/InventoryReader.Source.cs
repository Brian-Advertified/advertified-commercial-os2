using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryReader
{
    public async Task<InventoryImportSourceContent> GetImportSourceAsync(ActorId actorId, TenantId tenantId,
        Guid importId, CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, MasterDataReferences.Permissions.InventoryImport, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(actorId, tenantId, cancellationToken);
        await supplierAccess.EnsureImportAccessAsync(actorId, tenantId, importId, cancellationToken);
        var source = await store.FindImportAsync(tenantId, importId, false, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory import access denied.");
        if (source.ProtectedObjectKey is null || source.ScanStatus != MasterDataCodes.MalwareScanStatuses.Clean)
            throw new InventoryExtractionUnavailableException();
        var content = await store.ObjectStore.ReadAsync(source.ProtectedObjectKey, cancellationToken);
        InventoryExtractionCompletionPolicy.VerifySource(content, source.SourceHash);
        await transaction.CommitAsync(cancellationToken);
        return new(content, source.FileName);
    }
}
