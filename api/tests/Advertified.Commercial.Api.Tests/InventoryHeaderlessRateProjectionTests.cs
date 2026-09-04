using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DoclingInventoryExtractionAdapterTests
{
    [Fact]
    public void HeaderlessDigitalRateRowsBecomeSeparateProducts()
    {
        var json = JsonSerializer.Serialize(new
        {
            texts = Array.Empty<object>(),
            tables = new[]
            {
                Table(
                    Cell("www.iol.co.za", 0, 0),
                    Cell("", 0, 1),
                    Cell("", 0, 2),
                    Cell("Home Page", 1, 0),
                    Cell(
                        "24 hours exclusivity, includes x1 partnered article",
                        1, 1),
                    Cell("R65 000", 1, 2),
                    Cell("News", 2, 0),
                    Cell("Entire News Section 24 hour exclusivity", 2, 1),
                    Cell("R120 000", 2, 2)),
            },
        });
        var request = new InventoryExtractionRequest(
            "Media Deck 2026 (1).pptx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            MasterDataCodes.DocumentClasses.Pptx,
            new string('a', 64),
            [1]);

        var rows = DoclingInventoryProjection.ReadRows(request, json);
        var provider = InventoryExtractionContract.Create(
            "docling", "test", InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash, json, rows);
        var contextual = InventorySourceContextProjection.Apply(
            request, provider);
        var candidates = contextual.Rows.Select(row =>
            InventoryCandidateNormalizer.Normalize(
                row,
                request.SourceHash,
                DateTimeOffset.UnixEpoch)).ToArray();

        Assert.Equal(2, candidates.Length);
        Assert.Equal("Home Page", candidates[0].Values.Name);
        Assert.Equal(6_500_000, candidates[0].Values.RateAmountMinor);
        Assert.Equal("ZAR", candidates[0].Values.Currency);
        Assert.Equal(
            MasterDataCodes.Channels.Digital,
            candidates[0].Values.Channel);
        Assert.Equal(
            MasterDataCodes.InventoryProductTypes.DigitalPlacement,
            candidates[0].Values.ProductType);
        Assert.Null(candidates[0].Values.RateType);
        Assert.All(candidates, candidate => Assert.Equal(
            "Volt.Africa", candidate.SupplierName));
    }

    [Fact]
    public void ExplicitPerPostRowIsClassifiedAsSocialWithoutRateBasisGuess()
    {
        var rows = new[]
        {
            new InventoryTableRow(
                1,
                new Dictionary<int, string>
                {
                    [0] = "IOL",
                    [1] = "R3 000 per post per platform",
                }),
        };

        var projected = InventoryHeaderlessRateTableProjection.Project(
            rows,
            0,
            row => "pptx:slide=10;table=1;row=" + row,
            (row, column) =>
                "pptx:slide=10;table=1;row=" + row +
                ";cell=" + column);
        var candidate = InventoryCandidateNormalizer.Normalize(
            Assert.Single(projected),
            new string('b', 64),
            DateTimeOffset.UnixEpoch);

        Assert.Equal("IOL", candidate.Values.Name);
        Assert.Equal(300_000, candidate.Values.RateAmountMinor);
        Assert.Equal(
            MasterDataCodes.Channels.Social,
            candidate.Values.Channel);
        Assert.Equal(
            MasterDataCodes.InventoryProductTypes.SocialPlacement,
            candidate.Values.ProductType);
        Assert.Null(candidate.Values.RateType);
    }
}
