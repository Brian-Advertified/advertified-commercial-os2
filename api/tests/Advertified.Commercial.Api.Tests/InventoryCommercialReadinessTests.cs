using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryCommercialReadinessTests
{
    [Fact]
    public void EvaluateDoesNotRequireSupplierVatNumberWhenVatFactsAreEvidenced()
    {
        var inventory = new PlanningInventoryRow(
            InventoryTenantId: Guid.NewGuid(),
            MarketplaceListingVersionId: null,
            ProductId: Guid.NewGuid(),
            ProductVersionId: Guid.NewGuid(),
            SupplierId: Guid.NewGuid(),
            Name: "Evidence-backed daily placement",
            Channel: MasterDataCodes.Channels.Digital,
            ProductType: MasterDataCodes.InventoryProductTypes.SocialPlacement,
            Geography: "South Africa",
            Latitude: null,
            Longitude: null,
            RateId: Guid.NewGuid(),
            RateType: MasterDataCodes.RateTypes.DayRate,
            Currency: MasterDataCodes.Currencies.Zar,
            RateAmountMinor: 1_050_000,
            EffectiveFrom: new DateOnly(2026, 7, 1),
            EffectiveTo: new DateOnly(2027, 6, 30),
            RateSource: "Rate card, page 4, daily social post",
            AvailabilityId: Guid.NewGuid(),
            Availability: MasterDataCodes.AvailabilityStatuses.Available,
            ObservedAtUtc: new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero),
            ValidUntilUtc: null,
            AvailabilitySource: "Source does not mark placement unavailable",
            UnavailablePeriodsJson: "[]",
            AudienceProfileJson: null,
            SupplierVatStatus: MasterDataCodes.VatStatuses.Registered,
            SupplierCommercialJson: null,
            VatTreatment: MasterDataCodes.VatTreatments.Exclusive,
            CommercialTermsJson: null,
            DeliverableJson: null,
            SpatialJson: null,
            LogoAssetId: null);

        var result = InventoryCommercialReadiness.Evaluate(inventory);

        Assert.Empty(result.EvidenceGaps);
        Assert.Null(result.SupplierVatNumber);
    }

    [Fact]
    public void CandidateValidationDoesNotRequireSupplierVatNumber()
    {
        var codes = new InventoryCodeSets(
            Set(MasterDataCodes.Channels.Digital),
            Set(MasterDataCodes.InventoryProductTypes.SocialPlacement),
            Set(MasterDataCodes.RateTypes.DayRate),
            Set(MasterDataCodes.Currencies.Zar),
            Set(MasterDataCodes.AvailabilityStatuses.Available),
            Set(), Set(), Set(MasterDataCodes.VatStatuses.Registered),
            Set(MasterDataCodes.VatTreatments.Exclusive));
        var terms = new InventoryCommercialTermsValues(
            MasterDataCodes.VatTreatments.Exclusive,
            new DateOnly(2026, 7, 1), new DateOnly(2027, 6, 30),
            null, null, null, null, [], [], [], null, null, null, null, 30);
        var values = new InventoryCandidateValues(
            "EWN-DAILY-SOCIAL", "Eyewitness News daily social post",
            MasterDataCodes.Channels.Digital,
            MasterDataCodes.InventoryProductTypes.SocialPlacement,
            "Eyewitness News", null, null, null,
            MasterDataCodes.RateTypes.DayRate, MasterDataCodes.Currencies.Zar,
            1_050_000, MasterDataCodes.AvailabilityStatuses.Available,
            null, null, SupplierCommercial: new(
                MasterDataCodes.VatStatuses.Registered, null, null, null, null, null),
            CommercialTerms: terms);

        var issues = InventoryCandidateValidator.Validate(values, codes);

        Assert.DoesNotContain(issues,
            issue => issue.FieldName == "supplierCommercial.vatNumber");
    }

    private static HashSet<string> Set(params string[] values) =>
        values.ToHashSet(StringComparer.Ordinal);
}
