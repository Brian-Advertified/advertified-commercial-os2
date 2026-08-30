using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Delivery;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Delivery;

public sealed class DeliveryProofCommands(
    DeliveryProofRecordStore store,
    CommandDispatcher dispatcher,
    IInventoryObjectStore objectStore,
    IInventoryMalwareScanner malwareScanner,
    TimeProvider timeProvider) : IDeliveryProofCommands
{
    public async Task<CommandResult<DeliveryProofView>> SubmitAsync(
        Guid campaignId,
        CommandEnvelope<SubmitDeliveryProofCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.DeliveryProofSubmit,
            token => SubmitOutcomeAsync(campaignId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<DeliveryProofView>(receipt);
    }

    public async Task<CommandResult<DeliveryProofView>> ReviewAsync(
        Guid campaignId,
        Guid proofId,
        CommandEnvelope<ReviewDeliveryProofCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.DeliveryProofReview,
            token => ReviewOutcomeAsync(campaignId, proofId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<DeliveryProofView>(receipt);
    }

    private async Task<CommandOutcome> SubmitOutcomeAsync(
        Guid campaignId,
        CommandEnvelope<SubmitDeliveryProofCommand> envelope,
        CancellationToken cancellationToken)
    {
        var source = await store.FindSourceAsync(
            campaignId, envelope.Command.BookingId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Delivery proof access denied.");
        var now = timeProvider.GetUtcNow();
        var proof = DeliveryProofInputPolicy.Prepare(envelope.Command, source, now);
        var scan = await malwareScanner.ScanAsync(proof.Content, cancellationToken);
        if (!scan.IsClean) throw new DeliveryProofFileRejectedException();
        var id = Guid.NewGuid();
        var objectKey = ObjectKey(source.BuyerTenantId, campaignId, id, proof.Sha256);
        var persistedId = await store.InsertAsync(
            id, source, proof, objectKey, envelope, now, cancellationToken);
        await objectStore.PutAsync(objectKey, proof.Content, proof.MediaType, cancellationToken);
        var view = (await store.FindAsync(persistedId, false, cancellationToken))?.ToView()
            ?? throw new InvalidOperationException("The delivery proof was not persisted.");
        return Outcome(
            envelope, view, MasterDataReferences.CommercialActions.DeliveryProofSubmitted,
            MasterDataReferences.CommercialEventTypes.DeliveryProofSubmitted, now);
    }

    private async Task<CommandOutcome> ReviewOutcomeAsync(
        Guid campaignId,
        Guid proofId,
        CommandEnvelope<ReviewDeliveryProofCommand> envelope,
        CancellationToken cancellationToken)
    {
        var proof = await store.FindAsync(proofId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Delivery proof access denied.");
        if (proof.CampaignId != campaignId)
            throw new UnauthorizedAccessException("Delivery proof access denied.");
        if (proof.Version != envelope.ExpectedVersion)
            throw new VersionConflictException();
        if (proof.Status != MasterDataCodes.LifecycleStatuses.Submitted)
            throw new DeliveryProofBlockedException();
        var now = timeProvider.GetUtcNow();
        var decision = envelope.Command.Approved
            ? MasterDataCodes.LifecycleStatuses.Approved
            : MasterDataCodes.LifecycleStatuses.Rejected;
        await store.ReviewAsync(
            proof, envelope, decision,
            DeliveryProofInputPolicy.ReviewReason(envelope.Command.Reason), now, cancellationToken);
        var view = (await store.FindAsync(proofId, false, cancellationToken))?.ToView()
            ?? throw new InvalidOperationException("The delivery proof was not persisted.");
        return Outcome(
            envelope, view, MasterDataReferences.CommercialActions.DeliveryProofReviewed,
            MasterDataReferences.CommercialEventTypes.DeliveryProofReviewed, now);
    }

    private static CommandOutcome Outcome<TCommand>(
        CommandEnvelope<TCommand> envelope,
        DeliveryProofView view,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset now)
        where TCommand : notnull => CommandOutcomeFactory.Create(
            envelope, view, view.Id, view.Version,
            MasterDataReferences.CommercialResourceTypes.DeliveryProof, action, eventType, now);

    private static string ObjectKey(
        Guid buyerTenantId,
        Guid campaignId,
        Guid proofId,
        string hash) =>
        $"protected/{buyerTenantId:N}/campaigns/{campaignId:N}/proof/{proofId:N}/{hash}";
}
