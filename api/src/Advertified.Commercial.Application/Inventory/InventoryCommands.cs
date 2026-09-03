using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Inventory;

public sealed record InventorySourceFile(
    string FileName,
    string DeclaredMediaType,
    byte[] Content);

public sealed record CreateInventoryImportCommand(
    string SupplierName,
    InventorySourceFile Source);

public sealed record ExecuteInventoryImportCommand;

public sealed record RetryInventoryExtractionCommand(string Reason);

public sealed record CancelInventoryExtractionCommand(string Reason);

public sealed record ReconcileInventoryExtractionCommand(
    string Reason,
    string? ExternalTaskId);

public sealed record ReviewInventoryCandidateCommand(
    string Decision,
    string? RejectionReason,
    string? Notes,
    InventoryCandidateValues? CorrectedValues);

public sealed record PublishInventoryImportCommand;

public sealed record RecordInventoryAvailabilityExceptionCommand(
    Guid ProductVersionId,
    string ExceptionType,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string SourceLocator,
    string EvidenceHash);

public sealed record UploadInventoryAssetCommand(
    Guid ProductVersionId,
    string AssetType,
    InventorySourceFile Source);

public sealed record ReviewInventoryAssetRightsCommand(
    string RightsStatus,
    string? RightsBasis,
    DateOnly? LicensedUntil,
    IReadOnlyList<string>? ScopeCodes = null,
    string TerritoryCode = "ZA",
    DateOnly? EffectiveOn = null,
    bool UntilRevoked = false,
    string? AttestorRole = null,
    string? EvidenceReference = null,
    string? EvidenceHash = null);

public sealed record SubmitInventoryEmbeddingCommand(
    Guid ProductVersionId,
    bool ForceBackfill = false);

public sealed record NominateInventorySemanticDuplicateCommand(
    Guid ProductVersionId,
    Guid PeerProductId,
    Guid PeerProductVersionId,
    string Reason);

public sealed record ReviewInventoryDuplicateCommand(
    string Decision,
    Guid? CanonicalProductId,
    string Reason);

public sealed record InventoryAudienceSegmentValue(
    string Label,
    decimal? SharePercent);

public sealed record InventoryAudienceMeasurementValue(
    string MetricType,
    decimal? Value,
    string? Unit,
    string? Universe,
    string? MeasurementSource,
    string? MeasurementPeriod,
    string? Methodology,
    string? Limitations);

public sealed record InventoryAudienceProfileValues(
    IReadOnlyList<InventoryAudienceSegmentValue> SpokenLanguages,
    IReadOnlyList<InventoryAudienceSegmentValue> UnderstoodLanguages,
    IReadOnlyList<InventoryAudienceSegmentValue> LifeStages,
    IReadOnlyList<InventoryAudienceSegmentValue> LsmSemSegments,
    string? TaxonomyName,
    string? TaxonomyVersion,
    string? Universe,
    string? MeasurementSource,
    string? MeasurementPeriod,
    string? Methodology,
    string? Limitations,
    IReadOnlyList<InventoryAudienceMeasurementValue>? Measurements = null);

public sealed record InventoryCandidateValues(
    string? ProductCode,
    string? Name,
    string? Channel,
    string? ProductType,
    string? Geography,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    string? RateType,
    string? Currency,
    long? RateAmountMinor,
    string? Availability,
    IReadOnlyDictionary<string, string>? Extension,
    InventoryAudienceProfileValues? AudienceProfile,
    string? Description = null,
    InventorySupplierCommercialValues? SupplierCommercial = null,
    IReadOnlyList<InventorySupplierContactValue>? SupplierContacts = null,
    InventoryCommercialTermsValues? CommercialTerms = null,
    InventoryDeliverableValues? Deliverable = null,
    InventorySpatialValues? Spatial = null,
    InventoryPackageValues? Package = null);

public interface IInventoryCommands
{
    Task<CommandResult<InventoryImportView>> CreateAsync(
        CommandEnvelope<CreateInventoryImportCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryImportView>> ExecuteAsync(
        Guid importId,
        CommandEnvelope<ExecuteInventoryImportCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryImportView>> RetryExtractionAsync(
        Guid importId,
        CommandEnvelope<RetryInventoryExtractionCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryImportView>> CancelExtractionAsync(
        Guid importId,
        CommandEnvelope<CancelInventoryExtractionCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryImportView>> ReconcileExtractionAsync(
        Guid importId,
        CommandEnvelope<ReconcileInventoryExtractionCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryCandidateView>> ReviewAsync(
        Guid candidateId,
        CommandEnvelope<ReviewInventoryCandidateCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryImportView>> PublishAsync(
        Guid importId,
        CommandEnvelope<PublishInventoryImportCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryAssetRightsReviewView>> ReviewAssetRightsAsync(
        Guid assetId,
        CommandEnvelope<ReviewInventoryAssetRightsCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryAvailabilityExceptionView>>
        RecordAvailabilityExceptionAsync(
            Guid productId,
            CommandEnvelope<RecordInventoryAvailabilityExceptionCommand> envelope,
            CancellationToken cancellationToken);

    Task<CommandResult<InventoryAssetView>> UploadAssetAsync(
        Guid productId,
        CommandEnvelope<UploadInventoryAssetCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryEmbeddingView>> SubmitEmbeddingAsync(
        Guid productId,
        CommandEnvelope<SubmitInventoryEmbeddingCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryDuplicateCandidateView>> NominateSemanticDuplicateAsync(
        Guid productId,
        CommandEnvelope<NominateInventorySemanticDuplicateCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryDuplicateCandidateView>> ReviewDuplicateAsync(
        Guid duplicateCandidateId,
        CommandEnvelope<ReviewInventoryDuplicateCommand> envelope,
        CancellationToken cancellationToken);
}
