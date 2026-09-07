using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryCandidateNormalizerTests
{
    [Fact]
    public void ExplicitProductCodeIsPreservedWithoutSupplierInference()
    {
        var sourceHash = new string('9', 64);
        var row = new InventoryExtractedRow(
            1,
            "pptx:slide=2;shape=4",
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["productcode"] = "Ab034",
                ["name"] = "Central Mall Portrait Screen",
            },
            MasterDataCodes.InventoryExtractionMethods.Ocr,
            0.95m,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["productcode"] = "pptx:slide=2;shape=3",
                ["name"] = "pptx:slide=2;shape=4",
            });
        var provider = InventoryExtractionContract.Create(
            "docling",
            "test",
            InventoryExtractionOptions.CurrentSchemaVersion,
            sourceHash,
            "{}",
            [row]);

        var candidate = InventoryCandidateNormalizer.Normalize(
            Assert.Single(provider.Rows),
            sourceHash,
            DateTimeOffset.UnixEpoch);

        Assert.Null(candidate.SupplierName);
        Assert.Equal("Ab034", candidate.Values.ProductCode);
        Assert.Equal("Central Mall Portrait Screen", candidate.Values.Name);
        Assert.Equal(MasterDataCodes.VatTreatments.Exclusive,
            candidate.Values.CommercialTerms!.VatTreatment);
        Assert.Contains(candidate.Evidence, item =>
            item.FieldName == "product_code" &&
            item.RawValue == "Ab034" &&
            item.NormalizedValue == "Ab034" &&
            item.SourceLocator == "pptx:slide=2;shape=3");
    }

    [Fact]
    public void WorkbookFieldsNormalizeOnlyExplicitCommercialEvidence()
    {
        var sourceHash = new string('a', 64);
        var row = new InventoryExtractedRow(
            1,
            "xlsx:sheet=Sheet1;table=1;row=2",
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = "GP01 - Central Mall Portrait Screen",
                ["baseprice"] = "15000",
                ["mediumtype"] = "OOH",
                ["city"] = "Johannesburg",
                ["country"] = "South Africa",
                ["currency"] = "ZAR",
                ["vatinclusive"] = "True",
                ["rateperiod"] = "monthly",
                ["termsconditionstext"] =
                    "OOH advertising terms apply. Rates are per month.",
            });
        var provider = InventoryExtractionContract.Create(
            "docling",
            "test",
            InventoryExtractionOptions.CurrentSchemaVersion,
            sourceHash,
            "{}",
            [row]);

        var candidate = InventoryCandidateNormalizer.Normalize(
            Assert.Single(provider.Rows),
            sourceHash,
            DateTimeOffset.UnixEpoch);

        Assert.Null(candidate.SupplierName);
        Assert.Equal(MasterDataCodes.Channels.Ooh, candidate.Values.Channel);
        Assert.Null(candidate.Values.ProductType);
        Assert.Equal("ZAR", candidate.Values.Currency);
        Assert.Equal(1_500_000, candidate.Values.RateAmountMinor);
        Assert.Equal(MasterDataCodes.RateTypes.MonthRate, candidate.Values.RateType);
        Assert.Equal(
            MasterDataCodes.VatTreatments.Inclusive,
            candidate.Values.CommercialTerms!.VatTreatment);
        Assert.Equal("Johannesburg", candidate.Values.Spatial!.Locality);
        Assert.Equal("South Africa", candidate.Values.Spatial.Country);
        Assert.Contains(
            "Rates are per month",
            candidate.Values.CommercialTerms.Conditions.Single(),
            StringComparison.Ordinal);
    }
}
