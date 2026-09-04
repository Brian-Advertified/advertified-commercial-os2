using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DoclingInventoryExtractionAdapterTests
{
    [Fact]
    public void OohPageCardReconstructsOneSiteFromSeparatedFacts()
    {
        var json = JsonSerializer.Serialize(new
        {
            texts = new object[]
            {
                PageText("M1 South before Smith off-ramp", 7),
                PageText("SITE NUMBER\nS0118KM", 7),
                PageText("Size:\n4m x 54m", 7),
                PageText("Illuminated:\nNone", 7),
                PageText("GPS Coordinate:\n-26.192257, 28.02755", 7),
                PageText("Audience Reach:\n1 010 796", 7),
                PageText("Impacts:\n7 509 964", 7),
                PageText("Availability:\nImmediately", 7),
                PageText("SITE INFO: This site reaches M1 traffic.", 7),
                PageText("R 90 000", 7),
                PageText("Rate Card", 7),
            },
            tables = Array.Empty<object>(),
        });
        var request = new InventoryExtractionRequest(
            "Kena Outdoor Site Inventory - African Bank September Avails.pdf",
            "application/pdf",
            MasterDataCodes.DocumentClasses.Pdf,
            new string('a', 64),
            [1]);

        var rows = DoclingInventoryProjection.ReadRows(request, json);
        var provider = InventoryExtractionContract.Create(
            "docling", "test", InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash, json, rows);
        var contextual = InventorySourceContextProjection.Apply(
            request, provider);
        var candidate = InventoryCandidateNormalizer.Normalize(
            Assert.Single(contextual.Rows),
            request.SourceHash,
            DateTimeOffset.UnixEpoch);

        Assert.Equal("Kena Outdoor", candidate.SupplierName);
        Assert.Equal("S0118KM", candidate.Values.ProductCode);
        Assert.Equal(
            "S0118KM - M1 South before Smith off-ramp",
            candidate.Values.Name);
        Assert.Equal(
            "M1 South before Smith off-ramp",
            candidate.Values.Address);
        Assert.Equal(MasterDataCodes.Channels.Ooh, candidate.Values.Channel);
        Assert.Equal(
            MasterDataCodes.InventoryProductTypes.OohSite,
            candidate.Values.ProductType);
        Assert.Equal("4 x 54", candidate.Values.Deliverable!.Dimensions);
        Assert.Equal(-26.192257m, candidate.Values.Latitude);
        Assert.Equal(28.02755m, candidate.Values.Longitude);
        Assert.Equal(
            MasterDataCodes.AvailabilityStatuses.Available,
            candidate.Values.Availability);
        Assert.Equal("ZAR", candidate.Values.Currency);
        Assert.Equal(9_000_000, candidate.Values.RateAmountMinor);
        Assert.Null(candidate.Values.RateType);
        Assert.Contains(
            "M1 traffic",
            candidate.Values.Description,
            StringComparison.Ordinal);
    }

    private static object PageText(string text, int page) => new
    {
        text,
        prov = new[] { new { page_no = page } },
    };
}
