namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record InventoryResearchCandidateRow(
    Guid ProductId, Guid ProductVersionId, string ProductName,
    string? OutletId, Guid SupplierId, string SupplierProductCode);

internal sealed record InventoryResearchPortfolioRow(
    Guid Id, string SourceLocator, string ObservationSourceLocatorsJson,
    decimal DeduplicatedReach, string Unit, string? ProductVersionIdsJson, string Status);

internal sealed record InventoryResearchProductRow(
    Guid ProductId, Guid CurrentVersionId, long Version, int VersionNumber);

internal sealed record InventoryResearchDatasetRow(
    Guid Id, Guid TenantId, string SourceName, string DatasetName,
    string MeasurementPeriod, string Methodology, string Universe,
    string RightsReference, string? TaxonomyName, string? TaxonomyVersion,
    string? Limitations, string Status, Guid CreatedBy,
    DateTimeOffset CreatedAtUtc, long Version);

internal sealed record InventoryResearchMatchRow(
    Guid Id, Guid DatasetId, Guid ObservationId, string SourceLocator,
    Guid? ProductId, Guid? ProductVersionId, string? ProductName,
    string? MatchBasis, string MatchDetail, string AudienceProfileJson,
    string Status, Guid CreatedBy, Guid? ReviewedBy, DateTimeOffset? ReviewedAtUtc,
    string? ReviewReason, Guid? AppliedProductVersionId, long Version);

internal sealed record InventoryResearchMarketplaceRow(
    Guid ListingId,
    Guid ListingVersionId,
    Guid ProductVersionId,
    int ListingVersionNumber,
    long ListingVersion);
