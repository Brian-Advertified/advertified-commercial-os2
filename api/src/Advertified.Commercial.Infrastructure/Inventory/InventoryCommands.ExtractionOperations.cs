using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands
{
    private async Task<CommandOutcome> RetryExtractionOutcomeAsync(
        Guid importId,
        CommandEnvelope<RetryInventoryExtractionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var (source, latest, provider) = await ReadOperatorContextAsync(
            importId, envelope.TenantId, cancellationToken);
        if (latest is null) throw new InvalidLifecycleTransitionException();
        await extractionAttemptStore.QueueRetryAsync(
            source, latest, envelope, provider, cancellationToken);
        return await BuildExtractionOutcomeAsync(
            source, envelope, MasterDataReferences.CommercialActions.InventoryExtractionRetryRequested,
            MasterDataReferences.CommercialEventTypes.InventoryExtractionRetryRequested,
            cancellationToken);
    }

    private async Task<CommandOutcome> CancelExtractionOutcomeAsync(
        Guid importId,
        CommandEnvelope<CancelInventoryExtractionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var (source, latest, _) = await ReadOperatorContextAsync(
            importId, envelope.TenantId, cancellationToken);
        if (latest is null) throw new InvalidLifecycleTransitionException();
        await extractionAttemptStore.CancelAsync(
            source, latest, envelope, cancellationToken);
        return await BuildExtractionOutcomeAsync(
            source, envelope, MasterDataReferences.CommercialActions.InventoryExtractionCancelled,
            MasterDataReferences.CommercialEventTypes.InventoryExtractionCancelled,
            cancellationToken);
    }

    private async Task<CommandOutcome> ReconcileExtractionOutcomeAsync(
        Guid importId,
        CommandEnvelope<ReconcileInventoryExtractionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var (source, latest, provider) = await ReadOperatorContextAsync(
            importId, envelope.TenantId, cancellationToken);
        await extractionAttemptStore.ReconcileAsync(
            source, latest, envelope, provider, cancellationToken);
        return await BuildExtractionOutcomeAsync(
            source, envelope, MasterDataReferences.CommercialActions.InventoryExtractionReconciled,
            MasterDataReferences.CommercialEventTypes.InventoryExtractionReconciled,
            cancellationToken);
    }

    private async Task<(InventoryImportRow Source, InventoryExtractionAttemptRow? Latest,
        IDurableInventoryDocumentExtractionAdapter Provider)> ReadOperatorContextAsync(
        Guid importId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var source = await store.FindImportAsync(tenantId, importId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory import access denied.");
        if (extractionAdapter is not IDurableInventoryDocumentExtractionAdapter provider)
            throw new InvalidLifecycleTransitionException();
        var latest = (await store.ListExtractionAttemptsAsync(
            tenantId, importId, cancellationToken)).FirstOrDefault();
        return (source, latest, provider);
    }

    private async Task<CommandOutcome> BuildExtractionOutcomeAsync<TCommand>(
        InventoryImportRow source,
        CommandEnvelope<TCommand> envelope,
        ActionCode action,
        EventTypeCode eventType,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var updated = await store.FindImportAsync(
            envelope.TenantId, source.Id, false, cancellationToken)
            ?? throw new InvalidOperationException("The inventory import was not persisted.");
        var view = await store.BuildImportViewAsync(updated, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, source.Id, updated.Version,
            MasterDataReferences.CommercialResourceTypes.InventoryImport,
            action, eventType, timeProvider.GetUtcNow());
    }
}
