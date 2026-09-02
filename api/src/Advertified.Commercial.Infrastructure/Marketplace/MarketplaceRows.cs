using System.Text.Json;
using Advertified.Commercial.Application.Marketplace;

namespace Advertified.Commercial.Infrastructure.Marketplace;

internal sealed record MarketplaceListingRow
{
    public Guid Id { get; set; }
    public Guid SupplierTenantId { get; set; }
    public Guid ProductId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ListingTerms { get; set; } = string.Empty;
    public long Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid? ListingVersionId { get; set; }
    public int? ListingVersionNumber { get; set; }
    public Guid? ProductVersionId { get; set; }
    public Guid? RateId { get; set; }
    public Guid? AvailabilityId { get; set; }
    public string? SupplierName { get; set; }
    public string? ProductName { get; set; }
    public string? Channel { get; set; }
    public string? ProductType { get; set; }
    public string? Geography { get; set; }
    public string? RateType { get; set; }
    public long? AmountMinor { get; set; }
    public string? Currency { get; set; }
    public string? Availability { get; set; }
    public DateTimeOffset? AvailabilityValidUntilUtc { get; set; }
    public string? Terms { get; set; }
    public Guid? PublishedBy { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
}

internal sealed record MarketplaceProductSnapshotRow
{
    public Guid ProductId { get; set; }
    public Guid ProductVersionId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid RateId { get; set; }
    public Guid AvailabilityId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string Geography { get; set; } = string.Empty;
    public string? AudienceProfileJson { get; set; }
    public string RateType { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateOnly? RateEffectiveFrom { get; set; }
    public DateOnly? RateEffectiveTo { get; set; }
    public string RateSourceLocator { get; set; } = string.Empty;
    public string AvailabilitySourceLocator { get; set; } = string.Empty;
    public string? SupplierVatStatus { get; set; }
    public string? SupplierCommercialJson { get; set; }
    public string? VatTreatment { get; set; }
    public string? CommercialTermsJson { get; set; }
    public string? DeliverableJson { get; set; }
    public string? SpatialJson { get; set; }
    public string? SpatialLocationGeoJson { get; set; }
    public string? CoverageGeometryGeoJson { get; set; }
    public string? CatchmentGeometryGeoJson { get; set; }
    public string? RouteGeometryGeoJson { get; set; }
    public Guid? LogoAssetId { get; set; }
    public string Availability { get; set; } = string.Empty;
    public DateTimeOffset? AvailabilityObservedAtUtc { get; set; }
    public DateTimeOffset? AvailabilityValidUntilUtc { get; set; }
}

internal sealed record MarketplaceRfqRow
{
    public Guid Id { get; set; }
    public Guid BuyerTenantId { get; set; }
    public Guid SupplierTenantId { get; set; }
    public Guid ListingVersionId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateOnly RequestedStart { get; set; }
    public DateOnly RequestedEnd { get; set; }
    public int Quantity { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public Guid? SentBy { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid? ResponseId { get; set; }
    public int? ResponseVersion { get; set; }
    public long? ResponseAmountMinor { get; set; }
    public string? ResponseCurrency { get; set; }
    public string? ResponseAvailability { get; set; }
    public string? ResponseTerms { get; set; }
    public DateTimeOffset? ResponseValidUntilUtc { get; set; }
    public string? EvidenceJson { get; set; }
    public Guid? SubmittedBy { get; set; }
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public Guid? AcceptedBy { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
}

internal static class MarketplaceRowMapper
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    internal static MarketplaceListingView ToView(this MarketplaceListingRow row)
    {
        MarketplaceListingVersionView? version = null;
        if (row.ListingVersionId.HasValue)
        {
            version = new MarketplaceListingVersionView(
                row.ListingVersionId.Value, row.ListingVersionNumber!.Value,
                row.ProductVersionId!.Value, row.RateId!.Value, row.AvailabilityId!.Value,
                row.SupplierName!, row.ProductName!, row.Channel!, row.ProductType!,
                row.Geography!, row.RateType!, row.AmountMinor!.Value, row.Currency!,
                row.Availability!, row.AvailabilityValidUntilUtc, row.Terms!,
                row.PublishedBy!.Value, row.PublishedAtUtc!.Value);
        }
        return new MarketplaceListingView(
            row.Id, row.SupplierTenantId, row.ProductId, row.Status, version,
            row.Version, row.UpdatedAtUtc);
    }

    internal static MarketplaceRfqView ToView(this MarketplaceRfqRow row)
    {
        MarketplaceResponseView? response = null;
        if (row.ResponseId.HasValue)
        {
            response = new MarketplaceResponseView(
                row.ResponseId.Value, row.Id, row.ResponseVersion!.Value,
                row.ResponseAmountMinor!.Value, row.ResponseCurrency!,
                row.ResponseAvailability!, row.ResponseTerms!,
                row.ResponseValidUntilUtc!.Value,
                JsonSerializer.Deserialize<string[]>(row.EvidenceJson ?? "[]", StoredJson) ?? [],
                row.SubmittedBy!.Value, row.SubmittedAtUtc!.Value,
                row.AcceptedBy, row.AcceptedAtUtc);
        }
        return new MarketplaceRfqView(
            row.Id, row.BuyerTenantId, row.SupplierTenantId, row.ListingVersionId,
            row.SupplierName, row.ProductName, row.Subject, row.RequestedStart,
            row.RequestedEnd, row.Quantity, row.DueAtUtc, row.Status, response,
            row.CreatedBy, row.SentBy, row.SentAtUtc, row.Version, row.UpdatedAtUtc);
    }
}
