using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Measurement;

public sealed class PerformanceEvidenceCommands(
    PerformanceEvidenceRecordStore store,
    CommandDispatcher dispatcher,
    IInventoryObjectStore objectStore,
    IInventoryMalwareScanner malwareScanner,
    TimeProvider timeProvider) : IPerformanceEvidenceCommands
{
    public async Task<CommandResult<PerformanceEvidenceView>> SubmitAsync(
        Guid campaignId,
        CommandEnvelope<SubmitPerformanceEvidenceCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.PerformanceFactSubmit,
            token => SubmitOutcomeAsync(campaignId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<PerformanceEvidenceView>(receipt);
    }

    public async Task<CommandResult<PerformanceEvidenceView>> ReviewAsync(
        Guid campaignId,
        Guid evidenceId,
        CommandEnvelope<ReviewPerformanceEvidenceCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.PerformanceFactReview,
            token => ReviewOutcomeAsync(campaignId, evidenceId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<PerformanceEvidenceView>(receipt);
    }

    private async Task<CommandOutcome> SubmitOutcomeAsync(
        Guid campaignId,
        CommandEnvelope<SubmitPerformanceEvidenceCommand> envelope,
        CancellationToken cancellationToken)
    {
        var source = await store.FindSourceAsync(
            campaignId, envelope.Command.ReviewerUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Performance evidence access denied.");
        var now = timeProvider.GetUtcNow();
        var evidence = PerformanceEvidenceInputPolicy.Prepare(envelope.Command, source, now);
        var scan = await malwareScanner.ScanAsync(evidence.Content, cancellationToken);
        if (!scan.IsClean) throw new PerformanceEvidenceFileRejectedException();
        var id = Guid.NewGuid();
        var objectKey = ObjectKey(source.TenantId, campaignId, id, evidence.Sha256);
        await store.InsertAsync(
            id, source, evidence, objectKey, envelope, now, cancellationToken);
        await objectStore.PutAsync(
            objectKey, evidence.Content, evidence.MediaType, cancellationToken);
        var view = await store.GetViewAsync(id, false, cancellationToken)
            ?? throw new InvalidOperationException("Performance evidence was not persisted.");
        return Outcome(
            envelope, view, MasterDataReferences.CommercialActions.PerformanceEvidenceSubmitted,
            MasterDataReferences.CommercialEventTypes.PerformanceEvidenceSubmitted, now);
    }

    private async Task<CommandOutcome> ReviewOutcomeAsync(
        Guid campaignId,
        Guid evidenceId,
        CommandEnvelope<ReviewPerformanceEvidenceCommand> envelope,
        CancellationToken cancellationToken)
    {
        var evidence = await store.FindAsync(evidenceId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Performance evidence access denied.");
        if (evidence.CampaignId != campaignId ||
            evidence.ReviewerUserId != envelope.ActorId.Value)
            throw new UnauthorizedAccessException("Performance evidence access denied.");
        if (evidence.Version != envelope.ExpectedVersion) throw new VersionConflictException();
        if (evidence.Status != MasterDataCodes.LifecycleStatuses.Submitted ||
            (envelope.Command.Approved &&
                evidence.QualityStatus == MasterDataCodes.MeasurementQualityStatuses.Unusable))
            throw new PerformanceEvidenceBlockedException();
        var now = timeProvider.GetUtcNow();
        var decision = envelope.Command.Approved
            ? MasterDataCodes.LifecycleStatuses.Approved
            : MasterDataCodes.LifecycleStatuses.Rejected;
        await store.ReviewAsync(
            evidence, envelope, decision,
            PerformanceEvidenceInputPolicy.ReviewReason(envelope.Command.Reason),
            now, cancellationToken);
        var view = await store.GetViewAsync(evidenceId, false, cancellationToken)
            ?? throw new InvalidOperationException("Performance evidence was not persisted.");
        return Outcome(
            envelope, view, MasterDataReferences.CommercialActions.PerformanceEvidenceReviewed,
            MasterDataReferences.CommercialEventTypes.PerformanceEvidenceReviewed, now);
    }

    private static CommandOutcome Outcome<TCommand>(
        CommandEnvelope<TCommand> envelope,
        PerformanceEvidenceView view,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset now)
        where TCommand : notnull => CommandOutcomeFactory.Create(
            envelope, view, view.Id, view.Version,
            MasterDataReferences.CommercialResourceTypes.PerformanceEvidence,
            action, eventType, now);

    private static string ObjectKey(
        Guid tenantId,
        Guid campaignId,
        Guid evidenceId,
        string hash) =>
        $"protected/{tenantId:N}/campaigns/{campaignId:N}/performance/{evidenceId:N}/{hash}";
}
