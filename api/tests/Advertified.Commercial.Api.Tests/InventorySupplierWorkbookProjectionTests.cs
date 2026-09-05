using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DoclingInventoryExtractionAdapterTests
{
    [Fact]
    public void WorkbookHeadersNormalizeOnlySuppliedCommercialFields()
    {
        var request = new InventoryExtractionRequest(
            "eleven8_inventory.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            MasterDataCodes.DocumentClasses.Xlsx,
            new string('a', 64),
            [1]);
        var row = new InventoryExtractedRow(
            1,
            "xlsx:sheet=Sheet1;table=1;row=2",
            new SortedDictionary<string, string>(
                StringComparer.Ordinal)
            {
                ["name"] =
                    "GP01 - Bedford Centre (Screen Portrait) (Bedfordview)",
                ["baseprice"] = "15000",
                ["mediumtype"] = "OOH",
                ["city"] = "Bedfordview",
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
            request.SourceHash,
            "{}",
            [row]);
        var contextual = provider;

        var candidate = InventoryCandidateNormalizer.Normalize(
            Assert.Single(contextual.Rows),
            request.SourceHash,
            DateTimeOffset.UnixEpoch);

        Assert.Null(candidate.SupplierName); // The filename is not supplier evidence.
        Assert.Equal(MasterDataCodes.Channels.Ooh, candidate.Values.Channel);
        Assert.Null(candidate.Values.ProductType); // Requires interpretation, not the filename.
        Assert.Equal("ZAR", candidate.Values.Currency);
        Assert.Equal(1_500_000, candidate.Values.RateAmountMinor);
        Assert.Equal(
            MasterDataCodes.RateTypes.MonthRate,
            candidate.Values.RateType);
        Assert.Equal(
            MasterDataCodes.VatTreatments.Inclusive,
            candidate.Values.CommercialTerms!.VatTreatment);
        Assert.Equal(
            "Bedfordview",
            candidate.Values.Spatial!.Locality);
        Assert.Equal(
            "South Africa",
            candidate.Values.Spatial.Country);
        Assert.Contains(
            "Rates are per month",
            candidate.Values.CommercialTerms.Conditions.Single(),
            StringComparison.Ordinal);
    }
}
