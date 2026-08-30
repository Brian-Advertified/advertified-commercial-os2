using System.Text.Json;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record InventoryImportRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
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
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string ExtensionJson { get; set; } = "{}";
    public string RateType { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public string RateLocator { get; set; } = string.Empty;
    public string Availability { get; set; } = string.Empty;
    public DateTimeOffset? ObservedAtUtc { get; set; }
    public DateTimeOffset? ValidUntilUtc { get; set; }
    public string AvailabilityLocator { get; set; } = string.Empty;
    public Guid SourceImportId { get; set; }
    public Guid SourceCandidateId { get; set; }
    public int VersionNumber { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
}

internal sealed record InventoryAssetRow
{
    public string AssetType { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string SourceReference { get; set; } = string.Empty;
}

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
            item.SourceLocator, item.SourceHash)).ToArray(),
        row.SourceLocator, row.ReviewedBy, row.Version, row.UpdatedAtUtc);

    internal static InventoryProductSummaryView ToView(this InventoryProductSummaryRow row) =>
        new(row.Id, row.SupplierId, row.SupplierName, row.ProductCode, row.Name,
            row.Channel, row.ProductType, row.Geography, row.Verification,
            row.Version, row.UpdatedAtUtc);
}
