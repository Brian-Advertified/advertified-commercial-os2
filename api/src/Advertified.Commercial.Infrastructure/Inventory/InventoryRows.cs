using System.Text.Json;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record InventoryImportRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierNameHint { get; set; }
    public string SupplierResolutionStatus { get; set; } = string.Empty;
    public string SupplierIdentityEvidenceJson { get; set; } = "{}";
    public string ReplacementMode { get; set; } = string.Empty;
    public Guid? PublishedReleaseId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string DeclaredMediaType { get; set; } = string.Empty;
    public string? DocumentClass { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ScanStatus { get; set; } = string.Empty;
    public string QuarantineObjectKey { get; set; } = string.Empty;
    public string? ProtectedObjectKey { get; set; }
    public string SourceHash { get; set; } = string.Empty;
    public long SourceSize { get; set; }
    public string? FailureCode { get; set; }
    public Guid CreatedBy { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed record InventoryImportStepRow
{
    public string StepType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}

internal sealed record InventoryCandidateRow
{
    public Guid Id { get; set; }
    public Guid? ProjectionId { get; set; }
    public Guid ImportId { get; set; }
    public int RowNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ValuesJson { get; set; } = "{}";
    public string ValidationJson { get; set; } = "[]";
    public string SourceLocator { get; set; } = string.Empty;
    public Guid? ReviewedBy { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed record InventoryCandidateCountsRow
{
    public int Total { get; set; }
    public int ReviewRequired { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Blocking { get; set; }
}

internal sealed record InventoryFieldEvidenceRow
{
    public Guid CandidateId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? RawValue { get; set; }
    public string? NormalizedValue { get; set; }
    public string Transformation { get; set; } = string.Empty;
    public string SourceLocator { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public string EvidenceBasis { get; set; } = string.Empty;
    public string VerificationState { get; set; } = string.Empty;
    public string RequiredAction { get; set; } = string.Empty;
    public DateTimeOffset CapturedAtUtc { get; set; }
    public DateOnly? EffectiveOn { get; set; }
    public DateOnly? FreshUntil { get; set; }
    public string ExtractionMethod { get; set; } = string.Empty;
    public decimal? ExtractionConfidence { get; set; }
}

internal sealed record InventoryProductSummaryRow
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string Geography { get; set; } = string.Empty;
    public string Verification { get; set; } = string.Empty;
    public long Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed record InventoryProductIdentityRow
{
    public Guid Id { get; set; }
    public long Version { get; set; }
}

internal sealed record InventoryProductDetailRow
{
    public Guid ProductVersionId { get; set; }
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string ExtensionJson { get; set; } = "{}";
    public string? AudienceProfileJson { get; set; }
    public string? Description { get; set; }
    public string? DeliverableJson { get; set; }
    public string? SpatialJson { get; set; }
    public string AudienceSourceLocator { get; set; } = string.Empty;
    public string RateType { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string? VatTreatment { get; set; }
    public string? CommercialTermsJson { get; set; }
    public string RateLocator { get; set; } = string.Empty;
    public string Availability { get; set; } = string.Empty;
    public DateTimeOffset? ObservedAtUtc { get; set; }
    public DateTimeOffset? ValidUntilUtc { get; set; }
    public string AvailabilityLocator { get; set; } = string.Empty;
    public Guid SourceImportId { get; set; }
    public Guid SourceCandidateId { get; set; }
    public int VersionNumber { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public int? SupplierVersionNumber { get; set; }
    public string? SupplierVatStatus { get; set; }
    public string? SupplierVatNumber { get; set; }
    public string? SupplierCommissionTerms { get; set; }
    public string? SupplierPaymentTerms { get; set; }
    public string? SupplierCancellationTerms { get; set; }
    public string? SupplierBookingDeadlineTerms { get; set; }
    public Guid? SupplierSourceImportId { get; set; }
    public DateTimeOffset? SupplierPublishedAtUtc { get; set; }
}

internal sealed record InventoryAssetRow
{
    public Guid Id { get; set; }
    public string AssetType { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string SourceReference { get; set; } = string.Empty;
    public string RightsStatus { get; set; } = string.Empty;
    public string? RightsBasis { get; set; }
    public DateOnly? LicensedUntil { get; set; }
    public long RightsVersion { get; set; }
    public string RightsScopesJson { get; set; } = "[]";
    public string TerritoryCode { get; set; } = "ZA";
    public DateOnly? EffectiveOn { get; set; }
    public bool UntilRevoked { get; set; }
}

internal sealed record InventorySupplierContactRow(
    Guid Id,
    string? Name,
    string? Role,
    string? Region,
    string? Email,
    string? Phone,
    string? Website,
    string? SocialHandle,
    DateTimeOffset ObservedAtUtc);

internal sealed record InventoryAvailabilityExceptionRow(
    Guid Id,
    Guid ProductId,
    Guid ProductVersionId,
    string ExceptionType,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string SourceLocator,
    string EvidenceHash,
    Guid RecordedBy,
    DateTimeOffset RecordedAtUtc);

internal sealed record InventoryPackageRow(
    Guid Id,
    string PackageCode,
    int VersionNumber,
    string Name,
    string? DiscountRule,
    string ConditionsJson,
    string ComponentProductCodesJson);

internal static class InventoryRowMapper
{
    internal static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    internal static InventoryCandidateView ToView(
        this InventoryCandidateRow row,
        IReadOnlyList<InventoryFieldEvidenceRow> evidence) => new(
        row.Id, row.ImportId, row.RowNumber, row.Status,
        JsonSerializer.Deserialize<InventoryCandidateValues>(row.ValuesJson, StoredJson)
            ?? throw new InvalidOperationException("Stored inventory values are invalid."),
        JsonSerializer.Deserialize<InventoryValidationIssueView[]>(
            row.ValidationJson, StoredJson) ?? [],
        evidence.Select(item => new InventoryFieldEvidenceView(
            item.FieldName, item.RawValue, item.NormalizedValue, item.Transformation,
            item.SourceLocator, item.SourceHash, item.EvidenceBasis,
            item.VerificationState, item.RequiredAction, item.CapturedAtUtc,
            item.EffectiveOn, item.FreshUntil, item.ExtractionMethod,
            item.ExtractionConfidence)).ToArray(),
        row.SourceLocator, row.ReviewedBy, row.Version, row.UpdatedAtUtc);

    internal static InventoryProductSummaryView ToView(this InventoryProductSummaryRow row) =>
        new(row.Id, row.SupplierId, row.SupplierName, row.ProductCode, row.Name,
            row.Channel, row.ProductType, row.Geography, row.Verification,
            row.Version, row.UpdatedAtUtc);
}
