using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Creative;

public sealed partial class CreativeCommands
{
    private async Task<CommandOutcome> CreateAssetOutcomeAsync(
        Guid campaignId,
        CommandEnvelope<CreateCreativeAssetCommand> envelope,
        CancellationToken cancellationToken)
    {
        var campaign = await campaignStore.FindAsync(campaignId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Campaign access denied.");
        if (campaign.Status != MasterDataCodes.LifecycleStatuses.CreativePending)
            throw new InvalidLifecycleTransitionException();
        if (campaign.Version != envelope.Command.CampaignVersion)
            throw new VersionConflictException();
        var requirement = await store.FindRequirementAsync(
            campaignId, envelope.Command.RequirementId, cancellationToken)
            ?? throw new CreativeReadinessBlockedException();
        if (await store.FindAssetForRequirementAsync(requirement.Id, cancellationToken) is not null)
            throw new CreativeReadinessBlockedException();
        var file = await PrepareFileAsync(envelope.Command.File, requirement, cancellationToken);
        var copy = CreativeInputPolicy.Copy(envelope.Command.ApprovedCopy);
        var now = timeProvider.GetUtcNow();
        var assetId = await store.CreateAssetShellAsync(
            requirement, envelope, now, cancellationToken);
        var asset = new CreativeAssetStateRow(
            assetId, envelope.TenantId.Value, campaignId, requirement.Id,
            requirement.SupplierTenantId, null, null, 0);
        var objectKey = ObjectKey(
            asset.BuyerTenantId, campaignId, assetId, 1, file.Sha256);
        await store.InsertVersionAsync(
            asset, requirement, envelope, file, copy, objectKey, now, cancellationToken);
        await objectStore.PutAsync(
            objectKey, file.Content, file.MediaType, cancellationToken);
        var view = await store.GetAssetViewAsync(campaignId, assetId, cancellationToken);
        return AssetOutcome(
            envelope, view, MasterDataReferences.CommercialActions.CreativeAssetVersionUploaded,
            MasterDataReferences.CommercialEventTypes.CreativeAssetVersionUploaded, now);
    }

    private async Task<CommandOutcome> UploadVersionOutcomeAsync(
        Guid campaignId,
        Guid assetId,
        CommandEnvelope<UploadCreativeAssetVersionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var asset = await CurrentAssetAsync(
            campaignId, assetId, envelope.ExpectedVersion, cancellationToken);
        var requirement = await store.FindRequirementAsync(
            campaignId, asset.RequirementId, cancellationToken)
            ?? throw new CreativeReadinessBlockedException();
        var file = await PrepareFileAsync(envelope.Command.File, requirement, cancellationToken);
        var copy = CreativeInputPolicy.Copy(envelope.Command.ApprovedCopy);
        var now = timeProvider.GetUtcNow();
        var nextVersion = checked((asset.CurrentVersionNumber ?? 0) + 1);
        var objectKey = ObjectKey(
            asset.BuyerTenantId, campaignId, assetId, nextVersion, file.Sha256);
        await store.InsertVersionAsync(
            asset, requirement, envelope, file, copy, objectKey, now, cancellationToken);
        await objectStore.PutAsync(
            objectKey, file.Content, file.MediaType, cancellationToken);
        var view = await store.GetAssetViewAsync(campaignId, assetId, cancellationToken);
        return AssetOutcome(
            envelope, view, MasterDataReferences.CommercialActions.CreativeAssetVersionUploaded,
            MasterDataReferences.CommercialEventTypes.CreativeAssetVersionUploaded, now);
    }

    private async Task<CreativeAssetStateRow> CurrentAssetAsync(
        Guid campaignId,
        Guid assetId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var asset = await store.FindAssetAsync(assetId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Creative asset access denied.");
        if (asset.CampaignId != campaignId)
            throw new UnauthorizedAccessException("Creative asset access denied.");
        if (asset.Version != expectedVersion) throw new VersionConflictException();
        return asset;
    }

    private async Task<PreparedCreativeFile> PrepareFileAsync(
        CreativeFileUpload upload,
        CreativeRequirementSourceRow requirement,
        CancellationToken cancellationToken)
    {
        var file = CreativeInputPolicy.PrepareFile(upload, requirement);
        var scan = await malwareScanner.ScanAsync(file.Content, cancellationToken);
        if (!scan.IsClean) throw new CreativeFileRejectedException();
        return file;
    }

    private static string ObjectKey(
        Guid tenantId,
        Guid campaignId,
        Guid assetId,
        long version,
        string hash) =>
        $"protected/{tenantId:N}/campaigns/{campaignId:N}/creative/{assetId:N}/{version}/{hash}";
}
