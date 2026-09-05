using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryReader
{
    private static InventoryProductView ToProductView(
        InventoryProductSummaryRow summary,
        InventoryProductDetailRow detail,
        IReadOnlyList<InventoryAssetRow> assets,
        IReadOnlyList<InventorySupplierContactRow> contacts,
        IReadOnlyList<InventoryPackageRow> packages,
        IReadOnlyList<InventoryAvailabilityExceptionRow> exceptions,
        DateTimeOffset now) => new(
        summary.ToView(), detail.ProductVersionId, detail.Address,
        detail.Latitude, detail.Longitude,
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            detail.ExtensionJson, InventoryRowMapper.StoredJson) ?? [],
        new InventoryRateView(
            detail.RateType, detail.Currency, detail.AmountMinor, detail.RateLocator,
            detail.EffectiveFrom, detail.EffectiveTo, detail.VatTreatment,
            Read<InventoryCommercialTermsValues>(detail.CommercialTermsJson)),
        new InventoryAvailabilityView(
            detail.Availability, detail.ObservedAtUtc, detail.ValidUntilUtc,
            detail.AvailabilityLocator),
        ToAudienceView(detail),
        assets.Select(item => new InventoryAssetView(
            item.AssetType, item.MediaType, item.ContentHash, item.SourceReference,
            item.Id, item.RightsStatus, item.RightsBasis, item.LicensedUntil,
            item.RightsStatus == MasterDataCodes.AssetRightsStatuses.Approved &&
            Read<string[]>(item.RightsScopesJson).Contains(
                MasterDataCodes.AssetRightsScopes.NamedClientProposal,
                StringComparer.Ordinal) &&
            item.EffectiveOn.HasValue && item.EffectiveOn <=
                DateOnly.FromDateTime(now.UtcDateTime) &&
            (item.UntilRevoked || item.LicensedUntil.HasValue &&
                item.LicensedUntil.Value >= DateOnly.FromDateTime(now.UtcDateTime)),
            item.RightsVersion,
            Read<string[]>(item.RightsScopesJson), item.TerritoryCode,
            item.EffectiveOn, item.UntilRevoked)).ToArray(),
        detail.SourceImportId, detail.SourceCandidateId,
        detail.VersionNumber, detail.PublishedAtUtc, detail.Description,
        ToSupplierCommercialView(detail), contacts.Select(item =>
            new InventorySupplierContactView(item.Id, item.Name, item.Role, item.Region,
                item.Email, item.Phone, item.Website, item.SocialHandle,
                item.ObservedAtUtc)).ToArray(),
        Read<InventoryDeliverableValues>(detail.DeliverableJson),
        Read<InventorySpatialValues>(detail.SpatialJson),
        packages.Select(ToPackageView).ToArray(),
        exceptions.Select(item => new InventoryAvailabilityExceptionView(
            item.Id, item.ProductId, item.ProductVersionId, item.ExceptionType,
            item.StartsOn, item.EndsOn, item.SourceLocator, item.EvidenceHash,
            item.RecordedBy, item.RecordedAtUtc, 1)).ToArray());

    private static InventorySupplierCommercialView? ToSupplierCommercialView(
        InventoryProductDetailRow detail) => !detail.SupplierVersionNumber.HasValue ||
        !detail.SupplierSourceImportId.HasValue || !detail.SupplierPublishedAtUtc.HasValue
        ? null
        : new(detail.SupplierVersionNumber.Value, detail.SupplierVatStatus,
            detail.SupplierVatNumber, detail.SupplierCommissionTerms,
            detail.SupplierPaymentTerms, detail.SupplierCancellationTerms,
            detail.SupplierBookingDeadlineTerms, detail.SupplierSourceImportId.Value,
            detail.SupplierPublishedAtUtc.Value);

    private static InventoryPackageView ToPackageView(InventoryPackageRow row) => new(
        row.Id, row.PackageCode, row.VersionNumber, row.Name, row.DiscountRule,
        JsonSerializer.Deserialize<string[]>(row.ConditionsJson,
            InventoryRowMapper.StoredJson) ?? [],
        JsonSerializer.Deserialize<string[]>(row.ComponentProductCodesJson,
            InventoryRowMapper.StoredJson) ?? []);

    private static T? Read<T>(string? json) where T : class =>
        json is null ? null : JsonSerializer.Deserialize<T>(
            json, InventoryRowMapper.StoredJson);

    private static InventoryAudienceProfileView? ToAudienceView(
        InventoryProductDetailRow detail)
    {
        if (detail.AudienceProfileJson is null)
        {
            return null;
        }
        var profile = JsonSerializer.Deserialize<InventoryAudienceProfileValues>(
            detail.AudienceProfileJson, InventoryRowMapper.StoredJson)
            ?? throw new InvalidOperationException("Stored audience profile is invalid.");
        return new InventoryAudienceProfileView(
            profile.SpokenLanguages, profile.UnderstoodLanguages,
            profile.LifeStages, profile.LsmSemSegments,
            profile.TaxonomyName, profile.TaxonomyVersion, profile.Universe,
            profile.MeasurementSource, profile.MeasurementPeriod,
            profile.Methodology, profile.Limitations, profile.Measurements ?? [],
            detail.AudienceSourceLocator);
    }
}
