using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Creative;

public sealed partial class CreativeCommands
{
    private async Task<CommandOutcome> ReviewBrandOutcomeAsync(
        Guid campaignId,
        Guid assetId,
        CommandEnvelope<ReviewCreativeBrandCommand> envelope,
        CancellationToken cancellationToken)
    {
        var asset = await CurrentAssetAsync(
            campaignId, assetId, envelope.ExpectedVersion, cancellationToken);
        var decision = Decision(envelope.Command.Approved);
        var rights = CreativeInputPolicy.Rights(
            envelope.Command.RightsStatus, envelope.Command.Approved);
        var now = timeProvider.GetUtcNow();
        await store.InsertReviewAsync(
            asset, envelope, MasterDataCodes.CreativeReviewTypes.BrandLegalRights,
            decision, rights, CreativeInputPolicy.Evidence(envelope.Command.EvidenceReference),
            CreativeInputPolicy.Reason(envelope.Command.Reason), now, cancellationToken);
        var view = await store.GetAssetViewAsync(campaignId, assetId, cancellationToken);
        return AssetOutcome(
            envelope, view, MasterDataReferences.CommercialActions.CreativeAssetBrandReviewed,
            MasterDataReferences.CommercialEventTypes.CreativeAssetBrandReviewed, now);
    }

    private async Task<CommandOutcome> ReviewSupplierOutcomeAsync(
        Guid assetId,
        CommandEnvelope<ReviewCreativeSupplierCommand> envelope,
        CancellationToken cancellationToken)
    {
        var asset = await store.FindAssetAsync(assetId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Creative asset access denied.");
        if (asset.Version != envelope.ExpectedVersion)
            throw new VersionConflictException();
        var now = timeProvider.GetUtcNow();
        await store.InsertReviewAsync(
            asset, envelope, MasterDataCodes.CreativeReviewTypes.SupplierTechnical,
            Decision(envelope.Command.Approved), null,
            CreativeInputPolicy.Evidence(envelope.Command.EvidenceReference),
            CreativeInputPolicy.Reason(envelope.Command.Reason), now, cancellationToken);
        var view = (await store.FindSupplierViewAsync(assetId, cancellationToken))?.ToView()
            ?? throw new UnauthorizedAccessException("Creative asset access denied.");
        return SupplierOutcome(
            envelope, view, MasterDataReferences.CommercialActions.CreativeAssetSupplierReviewed,
            MasterDataReferences.CommercialEventTypes.CreativeAssetSupplierReviewed, now);
    }

    private static string Decision(bool approved) => approved
        ? MasterDataCodes.LifecycleStatuses.Approved
        : MasterDataCodes.LifecycleStatuses.Rejected;

    private static CommandOutcome AssetOutcome<TCommand>(
        CommandEnvelope<TCommand> envelope,
        CreativeAssetView view,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset now)
        where TCommand : notnull => CommandOutcomeFactory.Create(
            envelope, view, view.Id, view.Version,
            MasterDataReferences.CommercialResourceTypes.CreativeAsset,
            action, eventType, now);

    private static CommandOutcome SupplierOutcome<TCommand>(
        CommandEnvelope<TCommand> envelope,
        SupplierCreativeAssetView view,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset now)
        where TCommand : notnull => CommandOutcomeFactory.Create(
            envelope, view, view.AssetId, view.Version,
            MasterDataReferences.CommercialResourceTypes.CreativeAsset,
            action, eventType, now);
}
