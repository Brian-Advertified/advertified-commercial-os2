using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Inventory;

public sealed record InventoryFieldEvidenceView(
    string FieldName,
    string? RawValue,
    string? NormalizedValue,
    string Transformation,
    string SourceLocator,
    string SourceHash);

public sealed record InventoryValidationIssueView(
    string FieldName,
    string Code,
    string Message,
    bool IsBlocking);

public sealed record InventoryCandidateView(
    Guid Id,
    Guid ImportId,
    int RowNumber,
    string Status,
    InventoryCandidateValues Values,
    IReadOnlyList<InventoryValidationIssueView> Validation,
    IReadOnlyList<InventoryFieldEvidenceView> Evidence,
    string SourceLocator,
    Guid? ReviewedBy,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record InventoryImportStepView(
    string StepType,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record InventoryCandidateCountsView(
    int Total,
    int ReviewRequired,
    int Approved,
    int Rejected,
    int Blocking);

public sealed record InventoryImportView(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    string FileName,
    string DeclaredMediaType,
    string? DocumentClass,
    string Status,
    string ScanStatus,
    string SourceHash,
    long SourceSize,
    string? FailureCode,
    IReadOnlyList<InventoryImportStepView> Steps,
    IReadOnlyList<InventoryCandidateView> Candidates,
    InventoryCandidateCountsView CandidateCounts,
    string? NextCandidateCursor,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record InventoryRateView(
    string RateType,
    string Currency,
    long AmountMinor,
    string SourceLocator);

public sealed record InventoryAvailabilityView(
    string Status,
    DateTimeOffset? ObservedAtUtc,
    DateTimeOffset? ValidUntilUtc,
    string SourceLocator);

public sealed record InventoryAssetView(
    string AssetType,
    string MediaType,
    string ContentHash,
    string SourceReference);

public sealed record InventoryProductSummaryView(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    string ProductCode,
    string Name,
    string Channel,
    string ProductType,
    string Geography,
    string Verification,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record InventoryProductView(
    InventoryProductSummaryView Product,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    IReadOnlyDictionary<string, string> Extension,
    InventoryRateView Rate,
    InventoryAvailabilityView Availability,
    IReadOnlyList<InventoryAssetView> Assets,
    Guid SourceImportId,
    Guid SourceCandidateId,
    int VersionNumber,
    DateTimeOffset PublishedAtUtc);

public sealed record InventoryProductPage(
    IReadOnlyList<InventoryProductSummaryView> Items,
    string? NextCursor,
    int MaximumSourceBytes);

public sealed record InventorySearchQuery(
    string? Search,
    string? Channel,
    string? Supplier,
    string? Geography,
    int PageSize,
    string? Cursor);

public interface IInventoryReader
{
    Task<InventoryImportView> GetImportAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid importId,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken);

    Task<InventoryProductPage> SearchAsync(
        ActorId actorId,
        TenantId tenantId,
        InventorySearchQuery query,
        CancellationToken cancellationToken);

    Task<InventoryProductView> GetProductAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid productId,
        CancellationToken cancellationToken);
}
