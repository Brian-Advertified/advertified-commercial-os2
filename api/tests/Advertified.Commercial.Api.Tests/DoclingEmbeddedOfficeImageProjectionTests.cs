using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DoclingInventoryExtractionAdapterTests
{
    [Fact]
    public async Task EmbeddedImageTableUsesLocalOcrBeforeSemanticEnrichment()
    {
        const string originalTask = "original-task";
        var paths = new List<string>();
        var submissions = 0;
        using var client = new HttpClient(new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            paths.Add(path);
            if (request.Method == HttpMethod.Post &&
                path == "/v1/convert/file")
            {
                submissions++;
                var body = request.Content!.ReadAsStringAsync()
                    .GetAwaiter().GetResult();
                if (body.Contains(
                        "embedded-office-image-1.png",
                        StringComparison.Ordinal))
                {
                    return JsonResponse(DmsPositioningImageResult());
                }
                Assert.Contains(
                    "embedded-office-image-2.png",
                    body,
                    StringComparison.Ordinal);
                return JsonResponse(DmsImageResult());
            }
            if (path == $"/v1/result/{originalTask}")
                return JsonResponse(EmptyDoclingResult());
            throw new InvalidOperationException(
                "Unexpected local Docling request: " + path);
        }))
        {
            BaseAddress = new Uri("http://docling.test"),
        };
        var adapter = new DoclingInventoryExtractionAdapter(
            client,
            Options.Create(new InventoryExtractionOptions
            {
                Mode = InventoryExtractionOptions.DoclingMode,
                BaseUrl = "http://docling.test",
                ApiKey = "local-contract-key",
            }));
        var sourceHash = new string('a', 64);
        var request = new InventoryExtractionRequest(
            "DMS Digital Rate Card.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            MasterDataCodes.DocumentClasses.Xlsx,
            sourceHash,
            ImageOnlySpreadsheet(2));

        var result = await adapter.ReadResultAsync(
            request, originalTask, CancellationToken.None);

        Assert.Equal(2, submissions);
        Assert.Equal(4, result.Rows.Count);
        Assert.DoesNotContain(
            result.Rows,
            row => NativeOfficeImageReader.IsRequired([row]));
        Assert.Contains(
            DoclingInventoryExtractionAdapter.EmbeddedImageProjectionVersion,
            result.AdapterVersion,
            StringComparison.Ordinal);
        Assert.Contains("/v1/convert/file", paths);
        Assert.DoesNotContain("/v1/convert/file/async", paths);
        Assert.All(result.Rows, row => Assert.StartsWith(
            "xlsx:sheet=Rates;image=2;cell=A2;",
            row.Locator,
            StringComparison.Ordinal));

        var candidates = result.Rows.Select(row =>
            InventoryCandidateNormalizer.Normalize(
                row,
                sourceHash,
                DateTimeOffset.Parse(
                    "2026-09-04T00:00:00Z",
                    CultureInfo.InvariantCulture))).ToArray();
        Assert.Equal(
            [
                "DStv Stream VOD",
                "DStv Stream VOD",
                "Dstv Stream Live",
                "You Tube",
            ],
            candidates.Select(item => item.Values.Name));
        Assert.All(candidates, item =>
        {
            Assert.Equal("DStv Media Sales", item.SupplierName);
            Assert.Equal("ZAR", item.Values.Currency);
            Assert.Null(item.Values.Channel); // Reader output precedes schema interpretation.
            Assert.Null(item.Values.ProductType);
            Assert.Null(item.Values.RateType);
            Assert.Null(item.Values.CommercialTerms);
            Assert.Equal("16 x 9", item.Values.Deliverable!.Dimensions);
        });
        // Context is retained for schema interpretation, never attached by a brand-name rule.
        Assert.All(candidates, item => Assert.Null(item.Values.Description));
        using var provider = JsonDocument.Parse(result.ProviderJson);
        Assert.Equal(2, provider.RootElement.GetProperty("embeddedOfficeImages").GetArrayLength());
        var structures = InventoryDocumentStructureReader.Read(sourceHash, result.ProviderJson);
        Assert.Contains(structures.Structures.SelectMany(item => item.Cells), cell =>
            cell.RawText == "Welcome to DStv on Digital" &&
            cell.Locator.StartsWith("xlsx:sheet=Rates;image=1;cell=A1;", StringComparison.Ordinal));
        Assert.Equal(57_500, candidates[0].Values.RateAmountMinor);
        Assert.Null(candidates[1].Values.RateAmountMinor);
        Assert.Equal(
            "AMBIGUOUS_TRUNCATED_RATE",
            candidates[1].Values.Extension!["rateambiguity"]);
        Assert.Equal(50_000, candidates[2].Values.RateAmountMinor);
        Assert.Equal(20_000, candidates[3].Values.RateAmountMinor);
    }

    [Theory]
    [InlineData(MasterDataCodes.DocumentClasses.Pdf)]
    [InlineData(MasterDataCodes.DocumentClasses.Xlsx)]
    public async Task RetainedReprojectionPreservesArtifactWithoutProviderCalls(string documentClass)
    {
        using var client = new HttpClient(new StubHandler(_ =>
            throw new InvalidOperationException("Retained reprojection must not submit or poll.")))
        {
            BaseAddress = new Uri("http://docling.test"),
        };
        var adapter = new DoclingInventoryExtractionAdapter(client,
            Options.Create(new InventoryExtractionOptions
            {
                Mode = InventoryExtractionOptions.DoclingMode,
                BaseUrl = "http://docling.test",
                ApiKey = "local-contract-key",
            }));
        var workbook = documentClass == MasterDataCodes.DocumentClasses.Xlsx;
        var request = new InventoryExtractionRequest(
            workbook ? "Supplier.xlsx" : "Supplier.pdf",
            workbook ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/pdf",
            documentClass, new string('d', 64),
            workbook ? ImageOnlySpreadsheet(1) : [1, 2, 3]);
        var retained = JsonSerializer.Serialize(new
        {
            texts = Array.Empty<object>(),
            tables = Array.Empty<object>(),
        });

        var result = await DoclingInventoryExtractionAdapter.ReprojectRetainedAsync(request, retained, CancellationToken.None);

        Assert.Equal(retained, result.ProviderJson);
        Assert.Equal(request.SourceHash, result.SourceHash);
        if (workbook)
            Assert.Contains(result.Rows, row => NativeOfficeImageReader.IsRequired([row]));
    }

    [Fact]
    public void RateCardTitleIsExplicitSupplierEvidence()
    {
        var json = JsonSerializer.Serialize(new
        {
            texts = new[]
            {
                new
                {
                    text = "DStv Media Sales Digital Rate Card",
                    prov = new[] { new { page_no = 1 } },
                    ocr_confidence = 0.99m,
                },
            },
            tables = new[]
            {
                Table(
                    Cell("Platform", 0, 0),
                    Cell("Rate", 0, 1),
                    Cell("DStv Stream VOD", 1, 0),
                    Cell("R575", 1, 1)),
            },
        });
        var request = new InventoryExtractionRequest(
            "embedded-office-image.png",
            "image/png",
            MasterDataCodes.DocumentClasses.Png,
            new string('b', 64),
            [1]);

        var row = Assert.Single(
            DoclingInventoryProjection.ReadRows(request, json));

        Assert.Equal("DStv Media Sales", row.Values["supplier"]);
        Assert.Equal(
            "docling:page=1;text=1;rate-card-title=1",
            row.FieldLocators!["supplier"]);
        Assert.False(row.Values.ContainsKey("ratetype"));
        Assert.False(row.Values.ContainsKey("channel"));
    }

    [Theory]
    [InlineData("Cost", false)]
    [InlineData("Package cost", true)]
    public void PriceOnlyDoesNotInventAFlatRate(
        string label,
        bool packageRateExpected)
    {
        var json = JsonSerializer.Serialize(new
        {
            texts = new[]
            {
                new
                {
                    text = "Video placement\n" + label + ": R575",
                    prov = new[] { new { page_no = 1 } },
                },
            },
            tables = Array.Empty<object>(),
        });
        var request = new InventoryExtractionRequest(
            "offer.png",
            "image/png",
            MasterDataCodes.DocumentClasses.Png,
            new string('c', 64),
            [1]);

        var row = Assert.Single(
            DoclingInventoryProjection.ReadRows(request, json));

        Assert.Equal("R575", row.Values["rate"]);
        Assert.Equal(
            packageRateExpected,
            row.Values.ContainsKey("ratetype"));
        if (packageRateExpected)
        {
            Assert.Equal(
                MasterDataCodes.RateTypes.PackageRate,
                row.Values["ratetype"]);
        }
    }

    private static object EmptyDoclingResult() => new
    {
        status = "success",
        document = new
        {
            json_content = new
            {
                texts = Array.Empty<object>(),
                tables = Array.Empty<object>(),
            },
        },
    };

    private static object DmsPositioningImageResult() => new
    {
        status = "success",
        document = new
        {
            json_content = new
            {
                texts = new[]
                {
                    new
                    {
                        text = "Welcome to DStv on Digital",
                        prov = new[] { new { page_no = 1 } },
                    },
                    new
                    {
                        text = "Live & VOD Options",
                        prov = new[] { new { page_no = 1 } },
                    },
                    new
                    {
                        text = "ACCESS ANYWHERE, ANY DEVICE, ANYTIME",
                        prov = new[] { new { page_no = 1 } },
                    },
                },
                tables = Array.Empty<object>(),
            },
        },
    };

    private static object DmsImageResult() => new
    {
        status = "success",
        document = new
        {
            json_content = new
            {
                texts = new[]
                {
                    new
                    {
                        text = "DStv Media Sales Digital Rate Card",
                        prov = new[] { new { page_no = 1 } },
                        ocr_confidence = 0.99m,
                    },
                },
                tables = new[]
                {
                    Table(
                        Cell("Platform (Select the Appropriate one )Ad Unit", 0, 0),
                        Cell("", 0, 1),
                        Cell("Width", 0, 2),
                        Cell("Height", 0, 3),
                        Cell("Format", 0, 4),
                        Cell("Rate", 0, 5),
                        Cell("Streaming", 1, 0),
                        Cell("Ad Unit", 1, 1),
                        Cell("Width", 1, 2),
                        Cell("Height", 1, 3),
                        Cell("Format", 1, 4),
                        Cell("Rate", 1, 5),
                        Cell("DStv Stream VOD", 2, 0),
                        Cell("Video Pre Roll", 2, 1),
                        Cell("", 2, 2),
                        Cell("16 9", 2, 3),
                        Cell("MP4 Skippable after 5 Seconds", 2, 4),
                        Cell("R575", 2, 5),
                        Cell("DStv Stream VOD", 3, 0),
                        Cell("Video Pre Roll", 3, 1),
                        Cell("", 3, 2),
                        Cell("16 9", 3, 3),
                        Cell("MP4 15 seconds non skip", 3, 4),
                        Cell("R1,10", 3, 5),
                        Cell("Dstv Stream Live", 4, 0),
                        Cell("Video", 4, 1),
                        Cell("", 4, 2),
                        Cell("16 9", 4, 3),
                        Cell("MP4", 4, 4),
                        Cell("R500", 4, 5),
                        Cell("You Tube", 5, 0),
                        Cell("Video Pre Roll", 5, 1),
                        Cell("", 5, 2),
                        Cell("16 9", 5, 3),
                        Cell("MP4", 5, 4),
                        Cell("R200", 5, 5)),
                },
            },
        },
    };

    private static HttpResponseMessage JsonResponse(object value) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(value),
                Encoding.UTF8,
                "application/json"),
        };
}
