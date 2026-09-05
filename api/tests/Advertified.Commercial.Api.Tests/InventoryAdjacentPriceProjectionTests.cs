using System.Text.Json;

using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DoclingInventoryExtractionAdapterTests
{
    [Fact]
    public void ProductNameAndFollowingPriceBlockBecomeOneOffer()
    {
        var json = JsonSerializer.Serialize(new
        {
            texts = new object[]
            {
                Text("Front Page Strip 10x8", 2),
                Text("R 56 800.00", 2),
                Text("Black & white", 2),
                Text("R 270.00", 2),
            },
            tables = Array.Empty<object>(),
        });
        var request = new InventoryExtractionRequest(
            "Arena-Business-Day-Rate-Sheet-2026-Repro.pdf",
            "application/pdf",
            MasterDataCodes.DocumentClasses.Pdf,
            new string('c', 64),
            [1]);

        var rows = DoclingInventoryProjection.ReadRows(request, json);
        var provider = InventoryExtractionContract.Create(
            "docling", "test",
            InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash, json, rows);
        var contextual = provider;
        var candidates = contextual.Rows.Select(row =>
            InventoryCandidateNormalizer.Normalize(
                row, request.SourceHash, DateTimeOffset.UnixEpoch)).ToArray();

        Assert.Equal(2, candidates.Length);
        Assert.Equal("Front Page Strip 10x8", candidates[0].Values.Name);
        Assert.Equal(5_680_000, candidates[0].Values.RateAmountMinor);
        Assert.Equal("Black & white", candidates[1].Values.Name);
        Assert.Equal(27_000, candidates[1].Values.RateAmountMinor);
        Assert.All(candidates, candidate =>
            Assert.Equal("ZAR", candidate.Values.Currency));
    }

    [Fact]
    public void PackageMonthlyCostSkipsExposureNumberBetweenNameAndRate()
    {
        var json = JsonSerializer.Serialize(new
        {
            texts = new object[]
            {
                Text("Cost Per Month\n24\nR37 380", 1),
            },
            tables = Array.Empty<object>(),
        });
        var request = new InventoryExtractionRequest(
            "Algoa FM - Algoa Club Package - Plan A - Generic & Sponsorship -2026.pdf",
            "application/pdf",
            MasterDataCodes.DocumentClasses.Pdf,
            new string('d', 64),
            [1]);

        var rows = DoclingInventoryProjection.ReadRows(request, json);
        var row = Assert.Single(rows);
        Assert.Equal(
            MasterDataCodes.RateTypes.MonthRate,
            row.Values["ratetype"]);
        var candidate = InventoryCandidateNormalizer.Normalize(
            row, request.SourceHash, DateTimeOffset.UnixEpoch);

        Assert.Equal("Cost Per Month", candidate.Values.Name);
        Assert.Equal(3_738_000, candidate.Values.RateAmountMinor);
        var rateEvidence = Assert.Single(
            candidate.Evidence,
            item => item.FieldName == "rate_type");
        Assert.Equal(
            MasterDataCodes.RateTypes.MonthRate,
            rateEvidence.NormalizedValue);
        Assert.Equal(MasterDataCodes.RateTypes.MonthRate,
            candidate.Values.RateType);
    }

    [Fact]
    public void RoadNumberIsNotProjectedAsACommercialRate()
    {
        var json = JsonSerializer.Serialize(new
        {
            texts = new object[]
            {
                Text("ISO 32 - R21 Freeway, towards Pretoria", 3),
            },
            tables = new[]
            {
                new
                {
                    prov = new[] { new { page_no = 3 } },
                    data = new
                    {
                        table_cells = new object[]
                        {
                            Cell("Name", 0, 0),
                            Cell("Rate", 0, 1),
                            Cell("ISO 32", 1, 0),
                            Cell("R90 000", 1, 1),
                        },
                    },
                },
            },
        });
        var request = new InventoryExtractionRequest(
            "Insight Outdoor ZA - Publisher Media Kit - 2025.pptx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            MasterDataCodes.DocumentClasses.Pptx,
            new string('e', 64),
            [1]);

        var rows = DoclingInventoryProjection.ReadRows(request, json);

        Assert.DoesNotContain(rows, row =>
            row.Values.GetValueOrDefault("rate") == "R21");
        Assert.Contains(rows, row =>
            row.Values.GetValueOrDefault("rate") == "R90 000");
    }

    private static object Text(string text, int page) => new
    {
        text,
        prov = new[] { new { page_no = page } },
    };
}
