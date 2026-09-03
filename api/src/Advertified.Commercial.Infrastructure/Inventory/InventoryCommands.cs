using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Foundation;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands(
    InventoryRecordStore store,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider,
    IOptions<InventoryProtectionOptions> protectionOptions,
    IInventoryDocumentExtractionAdapter extractionAdapter,
    InventoryExtractionAttemptStore extractionAttemptStore,
    IInventoryEmbeddingGenerator embeddingGenerator,
    IOptions<InventoryEmbeddingOptions> embeddingOptionsAccessor,
    InventoryDuplicatePolicy duplicatePolicy) : IInventoryCommands
{
    private readonly int maximumSourceBytes = protectionOptions.Value.MaximumSourceBytes;
    private readonly InventoryEmbeddingOptions embeddingOptions = embeddingOptionsAccessor.Value;

    public async Task<CommandResult<InventoryImportView>> CreateAsync(
        CommandEnvelope<CreateInventoryImportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.InventoryImport,
            token => CreateOutcomeAsync(envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryImportView>(receipt);
    }

    public async Task<CommandResult<InventoryImportView>> ExecuteAsync(
        Guid importId,
        CommandEnvelope<ExecuteInventoryImportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.InventoryImport,
            token => ExecuteOutcomeAsync(importId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryImportView>(receipt);
    }

    public async Task<CommandResult<InventoryImportView>> RetryExtractionAsync(
        Guid importId,
        CommandEnvelope<RetryInventoryExtractionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.InventoryImport,
            token => RetryExtractionOutcomeAsync(importId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryImportView>(receipt);
    }

    public async Task<CommandResult<InventoryImportView>> CancelExtractionAsync(
        Guid importId,
        CommandEnvelope<CancelInventoryExtractionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.InventoryImport,
            token => CancelExtractionOutcomeAsync(importId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryImportView>(receipt);
    }

    public async Task<CommandResult<InventoryImportView>> ReconcileExtractionAsync(
        Guid importId,
        CommandEnvelope<ReconcileInventoryExtractionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.InventoryImport,
            token => ReconcileExtractionOutcomeAsync(importId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryImportView>(receipt);
    }

    public async Task<CommandResult<InventoryCandidateView>> ReviewAsync(
        Guid candidateId,
        CommandEnvelope<ReviewInventoryCandidateCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.InventoryReview,
            token => ReviewOutcomeAsync(candidateId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryCandidateView>(receipt);
    }

    public async Task<CommandResult<InventoryImportView>> PublishAsync(
        Guid importId,
        CommandEnvelope<PublishInventoryImportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.InventoryPublish,
            token => PublishOutcomeAsync(importId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryImportView>(receipt);
    }

    public async Task<CommandResult<InventoryAssetRightsReviewView>> ReviewAssetRightsAsync(
        Guid assetId,
        CommandEnvelope<ReviewInventoryAssetRightsCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.InventoryAssetRightsReview,
            token => ReviewAssetRightsOutcomeAsync(assetId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryAssetRightsReviewView>(receipt);
    }

    public async Task<CommandResult<InventoryAvailabilityExceptionView>>
        RecordAvailabilityExceptionAsync(
            Guid productId,
            CommandEnvelope<RecordInventoryAvailabilityExceptionCommand> envelope,
            CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.InventoryPublish,
            token => RecordAvailabilityExceptionOutcomeAsync(productId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryAvailabilityExceptionView>(receipt);
    }

    public async Task<CommandResult<InventoryEmbeddingView>> SubmitEmbeddingAsync(
        Guid productId,
        CommandEnvelope<SubmitInventoryEmbeddingCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.InventoryReview,
            token => SubmitEmbeddingOutcomeAsync(productId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryEmbeddingView>(receipt);
    }

    public async Task<CommandResult<InventoryAssetView>> UploadAssetAsync(
        Guid productId,
        CommandEnvelope<UploadInventoryAssetCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.InventoryImport,
            token => UploadAssetOutcomeAsync(productId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryAssetView>(receipt);
    }

    public async Task<CommandResult<InventoryDuplicateCandidateView>>
        NominateSemanticDuplicateAsync(
            Guid productId,
            CommandEnvelope<NominateInventorySemanticDuplicateCommand> envelope,
            CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.InventoryReview,
            token => NominateSemanticDuplicateOutcomeAsync(productId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryDuplicateCandidateView>(receipt);
    }

    public async Task<CommandResult<InventoryDuplicateCandidateView>> ReviewDuplicateAsync(
        Guid duplicateCandidateId,
        CommandEnvelope<ReviewInventoryDuplicateCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.InventoryReview,
            token => ReviewDuplicateOutcomeAsync(duplicateCandidateId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryDuplicateCandidateView>(receipt);
    }
}
