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
    public string? OutletId { get; set; }
    public string? OutletName { get; set; }
    public string? OutletBasis { get; set; }
    public string? OutletSourceLocator { get; set; }
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

internal sealed record MarketplaceResponseIdentityRow
{
    public Guid RfqId { get; set; }
    public Guid BuyerTenantId { get; set; }
    public Guid SupplierTenantId { get; set; }
    public int ResponseVersion { get; set; }
}

internal sealed record MarketplaceResponseHistoryRow
{
    public Guid Id { get; set; }
    public Guid RfqId { get; set; }
    public int ResponseVersion { get; set; }
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Availability { get; set; } = string.Empty;
    public string Terms { get; set; } = string.Empty;
    public DateTimeOffset ValidUntilUtc { get; set; }
    public string EvidenceJson { get; set; } = "[]";
    public Guid SubmittedBy { get; set; }
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public Guid? AcceptedBy { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
}

internal sealed record MarketplaceRfqRow
{
    public Guid Id { get; set; }
    public Guid BuyerTenantId { get; set; }
    public Guid SupplierTenantId { get; set; }
    public Guid ListingVersionId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public long ListedAmountMinor { get; set; }
    public string ListedCurrency { get; set; } = string.Empty;
    public int QuoteVersionCount { get; set; }
    public long? FirstQuoteAmountMinor { get; set; }
    public string? FirstQuoteCurrency { get; set; }
    public int? AcceptedResponseVersion { get; set; }
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

    internal static MarketplaceResponseView ToView(this MarketplaceResponseHistoryRow row) => new(
        row.Id, row.RfqId, row.ResponseVersion, row.AmountMinor, row.Currency,
        row.Availability, row.Terms, row.ValidUntilUtc,
        JsonSerializer.Deserialize<string[]>(row.EvidenceJson, StoredJson) ?? [],
        row.SubmittedBy, row.SubmittedAtUtc, row.AcceptedBy, row.AcceptedAtUtc);

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
            row.CreatedBy, row.SentBy, row.SentAtUtc, row.Version, row.UpdatedAtUtc,
            Negotiation(row));
    }

    private static MarketplaceNegotiationSummaryView? Negotiation(MarketplaceRfqRow row)
    {
        if (row.QuoteVersionCount == 0) return null;
        var accepted = row.AcceptedAtUtc.HasValue;
        return new MarketplaceNegotiationSummaryView(
            row.QuoteVersionCount,
            row.ListedAmountMinor,
            row.ListedCurrency,
            row.FirstQuoteAmountMinor,
            row.FirstQuoteCurrency,
            row.ResponseAmountMinor,
            row.ResponseCurrency,
            accepted ? row.ResponseAmountMinor : null,
            accepted ? row.ResponseCurrency : null,
            Variance(row.FirstQuoteAmountMinor, row.FirstQuoteCurrency,
                row.ResponseAmountMinor, row.ResponseCurrency),
            accepted ? Variance(row.FirstQuoteAmountMinor, row.FirstQuoteCurrency,
                row.ResponseAmountMinor, row.ResponseCurrency) : null,
            row.AcceptedResponseVersion,
            ComparabilityLimitation(row, accepted));
    }

    private static decimal? Variance(long? basis, string? basisCurrency, long? value, string? valueCurrency)
    {
        if (!basis.HasValue || basis == 0 || !value.HasValue ||
            !string.Equals(basisCurrency, valueCurrency, StringComparison.Ordinal)) return null;
        return decimal.Round(100m * (value.Value - basis.Value) / basis.Value, 4);
    }

    private static string? ComparabilityLimitation(MarketplaceRfqRow row, bool accepted)
    {
        var limitations = new List<string>();
        if (row.FirstQuoteAmountMinor == 0)
            limitations.Add("Percentage variance is unavailable because the first quote amount is zero.");
        if (!string.Equals(row.FirstQuoteCurrency, row.ResponseCurrency, StringComparison.Ordinal))
            limitations.Add("First and current supplier quotes use different currencies and are not percentage-comparable.");
        if (accepted && !string.Equals(row.FirstQuoteCurrency, row.ResponseCurrency, StringComparison.Ordinal))
            limitations.Add("First and accepted supplier quotes use different currencies and are not percentage-comparable.");
        return limitations.Count == 0 ? null : string.Join(" ", limitations.Distinct(StringComparer.Ordinal));
    }
}
