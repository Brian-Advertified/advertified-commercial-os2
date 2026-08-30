using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Campaign;
using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Campaign;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Creative;

public sealed partial class CreativeCommands(
    CreativeRecordStore store,
    CampaignRecordStore campaignStore,
    CommandDispatcher dispatcher,
    IInventoryObjectStore objectStore,
    IInventoryMalwareScanner malwareScanner,
    TimeProvider timeProvider) : ICreativeCommands
{
    public async Task<CommandResult<CampaignView>> RequestAsync(
        Guid campaignId,
        CommandEnvelope<RequestCampaignCreativeCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.CampaignRequestCreative,
            token => RequestOutcomeAsync(campaignId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<CampaignView>(receipt);
    }

    public async Task<CommandResult<CreativeAssetView>> CreateAssetAsync(
        Guid campaignId,
        CommandEnvelope<CreateCreativeAssetCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.CreativeUpload,
            token => CreateAssetOutcomeAsync(campaignId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<CreativeAssetView>(receipt);
    }

    public async Task<CommandResult<CreativeAssetView>> UploadVersionAsync(
        Guid campaignId,
        Guid assetId,
        CommandEnvelope<UploadCreativeAssetVersionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.CreativeUpload,
            token => UploadVersionOutcomeAsync(campaignId, assetId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<CreativeAssetView>(receipt);
    }

    public async Task<CommandResult<CreativeAssetView>> ReviewBrandAsync(
        Guid campaignId,
        Guid assetId,
        CommandEnvelope<ReviewCreativeBrandCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.CreativeBrandReview,
            token => ReviewBrandOutcomeAsync(campaignId, assetId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<CreativeAssetView>(receipt);
    }

    public async Task<CommandResult<SupplierCreativeAssetView>> ReviewSupplierAsync(
        Guid assetId,
        CommandEnvelope<ReviewCreativeSupplierCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.CreativeSupplierReview,
            token => ReviewSupplierOutcomeAsync(assetId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<SupplierCreativeAssetView>(receipt);
    }

    public async Task<CommandResult<CampaignView>> ApproveCampaignAsync(
        Guid campaignId,
        CommandEnvelope<ApproveCampaignCreativeCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.CampaignApproveCreative,
            token => ApproveCampaignOutcomeAsync(campaignId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<CampaignView>(receipt);
    }
}
