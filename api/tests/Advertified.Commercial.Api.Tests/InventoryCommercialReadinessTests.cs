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
            SupplierName: "Evidence-backed supplier",
            Name: "Evidence-backed daily placement",
            Channel: MasterDataCodes.Channels.Social,
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

        var unspecified = InventoryCommercialReadiness.Evaluate(inventory with
        {
            RateType = MasterDataCodes.RateTypes.UnspecifiedPeriodRate,
        });
        Assert.Contains("inventory.rate.buyingUnit", unspecified.EvidenceGaps);
    }

    [Fact]
    public void CandidateValidationDoesNotRequireSupplierVatNumber()
    {
        var codes = new InventoryCodeSets(
            Set(MasterDataCodes.Channels.Social),
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
            MasterDataCodes.Channels.Social,
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

    [Fact]
    public void CandidateValidationDoesNotInvalidateVatSilence()
    {
        var codes = new InventoryCodeSets(
            Set(MasterDataCodes.Channels.Social),
            Set(MasterDataCodes.InventoryProductTypes.SocialPlacement),
            Set(MasterDataCodes.RateTypes.DayRate), Set(MasterDataCodes.Currencies.Zar),
            Set(MasterDataCodes.AvailabilityStatuses.Available), Set(), Set(), Set(),
            Set(MasterDataCodes.VatTreatments.Exclusive));
        var values = new InventoryCandidateValues(
            "SOURCE-1", "Source-priced placement", MasterDataCodes.Channels.Social,
            MasterDataCodes.InventoryProductTypes.SocialPlacement, "Website", null, null, null,
            MasterDataCodes.RateTypes.DayRate, MasterDataCodes.Currencies.Zar, 100_000,
            MasterDataCodes.AvailabilityStatuses.Available, null, null, null, null);

        var issues = InventoryCandidateValidator.Validate(values, codes);

        Assert.DoesNotContain(issues,
            issue => issue.FieldName.Contains("vat", StringComparison.OrdinalIgnoreCase) ||
                issue.Message.Contains("vat", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EvaluateDefaultsVatSilenceToExclusiveWithoutInvalidatingInventory()
    {
        var inventory = CreateReadyInventory() with
        {
            SupplierVatStatus = null,
            VatTreatment = null,
        };

        var result = InventoryCommercialReadiness.Evaluate(inventory);

        Assert.Equal(MasterDataCodes.VatTreatments.Exclusive, result.VatTreatment);
        Assert.DoesNotContain(result.EvidenceGaps, gap => gap.Contains("vat",
            StringComparison.OrdinalIgnoreCase));
    }

    private static PlanningInventoryRow CreateReadyInventory() => new(
        InventoryTenantId: Guid.NewGuid(), MarketplaceListingVersionId: null,
        ProductId: Guid.NewGuid(), ProductVersionId: Guid.NewGuid(), SupplierId: Guid.NewGuid(),
        SupplierName: "Source-priced supplier", Name: "Source-priced placement", Channel: MasterDataCodes.Channels.Social,
        ProductType: MasterDataCodes.InventoryProductTypes.SocialPlacement,
        Geography: "South Africa", Latitude: null, Longitude: null, RateId: Guid.NewGuid(),
        RateType: MasterDataCodes.RateTypes.DayRate, Currency: MasterDataCodes.Currencies.Zar,
        RateAmountMinor: 100_000, EffectiveFrom: new DateOnly(2026, 1, 1),
        EffectiveTo: new DateOnly(2026, 12, 31), RateSource: "Rate card page 1",
        AvailabilityId: Guid.NewGuid(), Availability: MasterDataCodes.AvailabilityStatuses.Available,
        ObservedAtUtc: null, ValidUntilUtc: null,
        AvailabilitySource: "Source does not mark unavailable", UnavailablePeriodsJson: "[]",
        AudienceProfileJson: null, SupplierVatStatus: null, SupplierCommercialJson: null,
        VatTreatment: null, CommercialTermsJson: null, DeliverableJson: null, SpatialJson: null,
        LogoAssetId: null);

    private static HashSet<string> Set(params string[] values) =>
        values.ToHashSet(StringComparer.Ordinal);
}
