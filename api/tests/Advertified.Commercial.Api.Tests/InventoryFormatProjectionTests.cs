using System.Globalization;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DoclingInventoryExtractionAdapterTests
{
    [Fact]
    public void VerticalOohKeyValueTableCreatesOneGroundedSite()
    {
        var json = JsonSerializer.Serialize(new
        {
            texts = Array.Empty<object>(),
            tables = new[]
            {
                Table(
                    Cell("Description", 0, 0),
                    Cell("The billboard is located on the N1 freeway.", 0, 1),
                    Cell("Area", 1, 0),
                    Cell("PTA - Centurion", 1, 1),
                    Cell("City/Prov.", 2, 0),
                    Cell("Tshwane, Gauteng", 2, 1),
                    Cell("Traffic Count", 3, 0),
                    Cell("139 602 per day", 3, 1),
                    Cell("Impacts", 4, 0),
                    Cell("5 355 224 per month", 4, 1),
                    Cell("Type", 5, 0),
                    Cell("15 Sec slot, 3 min frequency", 5, 1),
                    Cell("Format", 6, 0),
                    Cell("4.5m x 18m Highway Digital", 6, 1),
                    Cell("Drivers Side", 7, 0),
                    Cell("Left", 7, 1),
                    Cell("GPS", 8, 0),
                    Cell("25°53'16.46\"S, 28°9'57.16\"E", 8, 1),
                    Cell("Rate Card", 9, 0),
                    Cell("R90 000", 9, 1),
                    Cell("Discounted Rate", 10, 0),
                    Cell("RR180 CPM", 10, 1),
                    Cell("Target Mall", 11, 0),
                    Cell("Centurion Mall", 11, 1)),
            },
        });
        var request = new InventoryExtractionRequest(
            "Insight Outdoor ZA - Publisher Media Kit - 2025.pptx",
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
        var candidate = InventoryCandidateNormalizer.Normalize(
            Assert.Single(contextual.Rows),
            request.SourceHash,
            DateTimeOffset.UnixEpoch);

        Assert.Equal("Insight Outdoor ZA", candidate.SupplierName);
        Assert.Equal(MasterDataCodes.Channels.Dooh, candidate.Values.Channel);
        Assert.Equal(
            MasterDataCodes.InventoryProductTypes.DoohScreen,
            candidate.Values.ProductType);
        Assert.Contains(
            "PTA - Centurion",
            candidate.Values.Name,
            StringComparison.Ordinal);
        Assert.Equal(
            "PTA - Centurion | Tshwane, Gauteng",
            candidate.Values.Geography);
        Assert.Equal(
            "4.5m x 18m Highway Digital",
            candidate.Values.Deliverable!.Format);
        Assert.Equal("4.5 x 18", candidate.Values.Deliverable.Dimensions);
        Assert.Equal("Centurion Mall", candidate.Values.Spatial!.Venue);
        Assert.Equal("ZAR", candidate.Values.Currency);
        Assert.Equal(9_000_000, candidate.Values.RateAmountMinor);
        Assert.Null(candidate.Values.RateType);
        Assert.Equal(
            -25.887906m,
            decimal.Round(candidate.Values.Latitude!.Value, 6));
        Assert.Equal(
            28.165878m,
            decimal.Round(candidate.Values.Longitude!.Value, 6));
    }

    [Fact]
    public void RadioRateTableExpandsEachDayAndTimeBand()
    {
        var json = JsonSerializer.Serialize(new
        {
            texts = new object[]
            {
                new
                {
                    text = "METRO FM is South Africa's largest national urban commercial radio station.",
                    prov = new[] { new { page_no = 5 } },
                },
                new
                {
                    text = "MONDAY - FRIDAY\nSATURDAY\nSUNDAY",
                    prov = new[] { new { page_no = 5 } },
                },
            },
            tables = new[]
            {
                new
                {
                    prov = new[] { new { page_no = 5 } },
                    data = new
                    {
                        table_cells = new object[]
                        {
                            Cell("TIME BAND", 0, 0),
                            Cell("NET RATES", 0, 1),
                            Cell("TIME BAND", 0, 2),
                            Cell("NET RATES", 0, 3),
                            Cell("TIME BAND", 0, 4),
                            Cell("NET RATES", 0, 5),
                            Cell("00:00-03:00", 1, 0),
                            Cell("900", 1, 1),
                            Cell("00:00-03:00", 1, 2),
                            Cell("720", 1, 3),
                            Cell("00:00-03:00", 1, 4),
                            Cell("960", 1, 5),
                            Cell("03:00-05:00", 2, 0),
                            Cell("960", 2, 1),
                            Cell("03:00-06:00", 2, 2),
                            Cell("1 530", 2, 3),
                            Cell("03:00-06:00", 2, 4),
                            Cell("1 290", 2, 5),
                        },
                    },
                },
            },
        });
        var request = new InventoryExtractionRequest(
            "SABC Radio Rates F2025-2026.pdf",
            "application/pdf",
            MasterDataCodes.DocumentClasses.Pdf,
            new string('b', 64),
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
                DateTimeOffset.Parse(
                    "2026-09-04T00:00:00Z",
                    CultureInfo.InvariantCulture))).ToArray();

        Assert.Equal(6, candidates.Length);
        var first = candidates[0];
        Assert.Equal("SABC", first.SupplierName);
        Assert.Equal(MasterDataCodes.Channels.Radio, first.Values.Channel);
        Assert.Equal(
            MasterDataCodes.InventoryProductTypes.RadioSpot,
            first.Values.ProductType);
        Assert.Equal(
            MasterDataCodes.RateTypes.SpotRate,
            first.Values.RateType);
        Assert.Equal("ZAR", first.Values.Currency);
        Assert.Equal(90_000, first.Values.RateAmountMinor);
        Assert.Equal(
            "METRO FM - MONDAY_FRIDAY - 00:00-03:00",
            first.Values.Name);
        Assert.Equal(
            "00:00-03:00",
            first.Values.Deliverable!.Daypart);
        Assert.Equal("Radio spot", first.Values.Deliverable.Placement);
        Assert.Equal(
            3,
            candidates.Count(candidate =>
                candidate.Values.Name!.Contains(
                    "00:00-03:00",
                    StringComparison.Ordinal)));
    }

    [Fact]
    public void PrintPricesAreReadEvenWhenUnrelatedTablesExist()
    {
        var json = JsonSerializer.Serialize(new
        {
            texts = new object[]
            {
                new
                {
                    text = "Front Page Strip 10x8\nR 56 800.00",
                    prov = new[] { new { page_no = 2 } },
                },
            },
            tables = new[]
            {
                new
                {
                    prov = new[] { new { page_no = 4 } },
                    data = new
                    {
                        table_cells = new object[]
                        {
                            Cell("For additional information", 0, 0),
                            Cell("Legals@arena.africa", 0, 1),
                        },
                    },
                },
            },
        });
        var request = new InventoryExtractionRequest(
            "Arena-Business-Day-Rate-Sheet-2026-Repro.pdf",
            "application/pdf",
            MasterDataCodes.DocumentClasses.Pdf,
            new string('c', 64),
            [1]);

        var rows = DoclingInventoryProjection.ReadRows(request, json);
        var provider = InventoryExtractionContract.Create(
            "docling", "test", InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash, json, rows);
        var contextual = InventorySourceContextProjection.Apply(
            request, provider);
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(
            contextual.Rows,
            request.SourceHash,
            "Not supplied",
            EmptyCodes(),
            DateTimeOffset.UnixEpoch);

        var candidate = Assert.Single(candidates);
        Assert.Contains(
            candidate.Evidence,
            item => item.FieldName == "supplier_name" &&
                item.RawValue == "Arena");
        Assert.Equal("Front Page Strip 10x8", candidate.Values.Name);
        Assert.Equal(MasterDataCodes.Channels.Print, candidate.Values.Channel);
        Assert.Equal(
            MasterDataCodes.InventoryProductTypes.PrintPlacement,
            candidate.Values.ProductType);
        Assert.Equal("ZAR", candidate.Values.Currency);
        Assert.Equal(5_680_000, candidate.Values.RateAmountMinor);
        Assert.Null(candidate.Values.RateType);
    }

    [Fact]
    public void SiteCodeSurvivesPoiOnlyTables()
    {
        var json = JsonSerializer.Serialize(new
        {
            texts = new object[]
            {
                new
                {
                    text = "CAPE TOWN CENTRAL: GARDENS",
                    prov = new[] { new { page_no = 2 } },
                },
                new
                {
                    text = "WCD001",
                    prov = new[] { new { page_no = 2 } },
                },
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
                            Cell("47m", 0, 0),
                            Cell("49m", 1, 0),
                            Cell("52m", 2, 0),
                        },
                    },
                },
            },
        });
        var request = new InventoryExtractionRequest(
            "RSD Rate Cards - Western Cape - 2025.pptx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            MasterDataCodes.DocumentClasses.Pptx,
            new string('d', 64),
            [1]);

        var rows = DoclingInventoryProjection.ReadRows(request, json);
        var provider = InventoryExtractionContract.Create(
            "docling", "test", InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash, json, rows);
        var contextual = InventorySourceContextProjection.Apply(
            request, provider);
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(
            contextual.Rows,
            request.SourceHash,
            "Not supplied",
            EmptyCodes(),
            DateTimeOffset.UnixEpoch);

        var candidate = Assert.Single(candidates);
        Assert.Equal("WCD001", candidate.Values.ProductCode);
        Assert.Contains(
            candidate.Evidence,
            item => item.FieldName == "supplier_name" &&
                item.RawValue == "RSD");
        Assert.Equal(MasterDataCodes.Channels.Dooh, candidate.Values.Channel);
        Assert.Equal(
            MasterDataCodes.InventoryProductTypes.DoohScreen,
            candidate.Values.ProductType);
    }
}
