using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Inventory;

public sealed record InventoryFieldEvidenceView(
    string FieldName,
    string? RawValue,
    string? NormalizedValue,
    string Transformation,
    string SourceLocator,
    string SourceHash,
    string EvidenceBasis,
    string VerificationState,
    string RequiredAction,
    DateTimeOffset CapturedAtUtc,
    DateOnly? EffectiveOn,
    DateOnly? FreshUntil,
    string ExtractionMethod,
    decimal? ExtractionConfidence);

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
    IReadOnlyList<InventoryExtractionAttemptView> ExtractionAttempts,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record InventoryRateView(
    string RateType,
    string Currency,
    long AmountMinor,
    string SourceLocator,
    DateOnly? EffectiveFrom = null,
    DateOnly? EffectiveTo = null,
    string? VatTreatment = null,
    InventoryCommercialTermsValues? CommercialTerms = null);

public sealed record InventoryAvailabilityView(
    string Status,
    DateTimeOffset? ObservedAtUtc,
    DateTimeOffset? ValidUntilUtc,
    string SourceLocator);

public sealed record InventoryAvailabilityExceptionView(
    Guid Id,
    Guid ProductId,
    Guid ProductVersionId,
    string ExceptionType,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string SourceLocator,
    string EvidenceHash,
    Guid RecordedBy,
    DateTimeOffset RecordedAtUtc,
    long Version);

public sealed record InventoryAudienceProfileView(
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
    IReadOnlyList<InventoryAudienceMeasurementValue> Measurements,
    string SourceLocator);

public sealed record InventoryAssetView(
    string AssetType,
    string MediaType,
    string ContentHash,
    string SourceReference,
    Guid? AssetId = null,
    string? RightsStatus = null,
    string? RightsBasis = null,
    DateOnly? LicensedUntil = null,
    bool ProposalEligible = false,
    long RightsVersion = 1,
    IReadOnlyList<string>? RightsScopes = null,
    string TerritoryCode = "ZA",
    DateOnly? EffectiveOn = null,
    bool UntilRevoked = false);

public sealed record InventoryAssetRightsReviewView(
    Guid AssetId,
    string RightsStatus,
    string? RightsBasis,
    DateOnly? LicensedUntil,
    Guid ReviewedBy,
    DateTimeOffset ReviewedAtUtc,
    long Version,
    IReadOnlyList<string>? ScopeCodes = null,
    string TerritoryCode = "ZA",
    DateOnly? EffectiveOn = null,
    bool UntilRevoked = false,
    string? AttestorRole = null,
    string? EvidenceReference = null,
    string? EvidenceHash = null);

public sealed record InventoryAssetContent(
    byte[] Content,
    string MediaType,
    string ContentHash);

public sealed record InventoryEmbeddingView(
    Guid Id,
    Guid ProductId,
    Guid ProductVersionId,
    string Provider,
    string Model,
    string InputHash,
    int Dimensions,
    DateTimeOffset CreatedAtUtc,
    long Version,
    Guid? JobId = null,
    int InputTokens = 0,
    long IncrementalCostUsdMicros = 0,
    long MonthlyCostUsdMicros = 0,
    long MonthlyBudgetUsdMicros = 0,
    bool BudgetAlert = false);

public sealed record InventorySemanticRecallView(
    Guid ProductId,
    Guid ProductVersionId,
    string Name,
    string Geography,
    decimal Similarity);

public sealed record InventoryDuplicateCandidateView(
    Guid Id,
    Guid LeftProductId,
    Guid RightProductId,
    Guid LeftProductVersionId,
    Guid RightProductVersionId,
    string LeftName,
    string RightName,
    string Method,
    decimal? Similarity,
    string EvidenceJson,
    string Status,
    Guid? CanonicalProductId,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewReason,
    long Version);

public sealed record InventorySupplierContactView(
    Guid Id,
    string? Name,
    string? Role,
    string? Region,
    string? Email,
    string? Phone,
    string? Website,
    string? SocialHandle,
    DateTimeOffset ObservedAtUtc);

public sealed record InventorySupplierCommercialView(
    int VersionNumber,
    string? VatStatus,
    string? VatNumber,
    string? CommissionTerms,
    string? PaymentTerms,
    string? CancellationTerms,
    string? BookingDeadlineTerms,
    Guid SourceImportId,
    DateTimeOffset PublishedAtUtc);

public sealed record InventoryPackageView(
    Guid Id,
    string PackageCode,
    int VersionNumber,
    string Name,
    string? DiscountRule,
    IReadOnlyList<string> Conditions,
    IReadOnlyList<string> ComponentProductCodes);

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
    Guid ProductVersionId,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    IReadOnlyDictionary<string, string> Extension,
    InventoryRateView Rate,
    InventoryAvailabilityView Availability,
    InventoryAudienceProfileView? AudienceProfile,
    IReadOnlyList<InventoryAssetView> Assets,
    Guid SourceImportId,
    Guid SourceCandidateId,
    int VersionNumber,
    DateTimeOffset PublishedAtUtc,
    string? Description = null,
    InventorySupplierCommercialView? SupplierCommercial = null,
    IReadOnlyList<InventorySupplierContactView>? SupplierContacts = null,
    InventoryDeliverableValues? Deliverable = null,
    InventorySpatialValues? Spatial = null,
    IReadOnlyList<InventoryPackageView>? Packages = null,
    IReadOnlyList<InventoryAvailabilityExceptionView>? AvailabilityExceptions = null);

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

    Task<IReadOnlyList<InventorySemanticRecallView>> GetSemanticRecallAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid productId,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InventoryDuplicateCandidateView>> ListDuplicateCandidatesAsync(
        ActorId actorId,
        TenantId tenantId,
        string? status,
        CancellationToken cancellationToken);

    Task<InventoryAssetContent> GetApprovedAssetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid assetId,
        CancellationToken cancellationToken);
}
