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
    public async Task NativeOfficeStructureEnrichesEmptyDoclingResult()
    {
        var response = JsonSerializer.Serialize(new
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
        });
        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    response, Encoding.UTF8, "application/json"),
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
        var hash = new string('b', 64);

        var spreadsheet = await adapter.ReadResultAsync(
            new InventoryExtractionRequest(
                "rates.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                MasterDataCodes.DocumentClasses.Xlsx,
                hash,
                Spreadsheet()),
            "xlsx-task",
            CancellationToken.None);
        var presentation = await adapter.ReadResultAsync(
            new InventoryExtractionRequest(
                "packages.pptx",
                "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                MasterDataCodes.DocumentClasses.Pptx,
                hash,
                PresentationDeck()),
            "pptx-task",
            CancellationToken.None);

        var sheetRow = Assert.Single(spreadsheet.Rows);
        Assert.Equal("ALG-001", sheetRow.Values["productcode"]);
        Assert.Equal("157500", sheetRow.Values["rate"]);
        Assert.Equal(
            "xlsx:sheet=Rates;table=1;row=2;cell=C",
            sheetRow.FieldLocators!["rate"]);
        Assert.Contains(
            NativeOfficeInventoryProjection.AdapterVersion,
            spreadsheet.AdapterVersion,
            StringComparison.Ordinal);

        Assert.Equal(2, presentation.Rows.Count);
        var offer = presentation.Rows[0];
        Assert.Equal(
            "Generic 30 second commercial",
            offer.Values["element"]);
        Assert.Equal("R291,060", offer.Values["value"]);
        Assert.Equal(
            "pptx:slide=1;table=1;row=2;cell=3",
            offer.FieldLocators!["value"]);
        var candidate = InventoryCandidateNormalizer.Normalize(
            offer, hash,
            DateTimeOffset.Parse(
                "2026-09-03T00:00:00Z",
                CultureInfo.InvariantCulture));
        Assert.Equal(29_106_000, candidate.Values.RateAmountMinor);
        Assert.Equal(
            "Generic 30 second commercial",
            candidate.Values.Name);
    }

    [Fact]
    public void NativeOfficeImagesRemainBoundedForLocalOcr()
    {
        var request = new InventoryExtractionRequest(
            "visual-rates.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            MasterDataCodes.DocumentClasses.Xlsx,
            new string('c', 64),
            ImageOnlySpreadsheet(21));
        var settings = new InventorySemanticOptions();

        var images = NativeOfficeImageReader.ReadContent(
            request, settings);

        Assert.Equal(21, images.Count);
        Assert.Equal(
            Enumerable.Range(1, 21),
            images.Select(image => image.Ordinal));
        Assert.All(images, image =>
        {
            Assert.InRange(
                image.Content.Length,
                1,
                settings.MaximumImageBytes);
            Assert.Equal(64, image.Sha256.Length);
        });
        Assert.StartsWith(
            "xlsx:sheet=Rates;image=1;cell=A1;",
            images[0].Locator,
            StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedOfficeAssetRetainsHumanReviewEvidence()
    {
        var request = new InventoryExtractionRequest(
            "mixed-images.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            MasterDataCodes.DocumentClasses.Xlsx,
            new string('d', 64),
            MixedImageSpreadsheet());
        var settings = new InventorySemanticOptions();

        var images = NativeOfficeImageReader.ReadContent(
            request, settings);
        var exclusions = NativeOfficeImageReader.ReadExclusions(
            request, settings);

        Assert.Single(images);
        var excluded = Assert.Single(exclusions);
        Assert.Equal("UNSUPPORTED_EMBEDDED_ASSET", excluded.Kind);
        Assert.Contains(
            "embedded-part=xl%2Fmedia%2Fvector.svg",
            excluded.Locator,
            StringComparison.Ordinal);
        Assert.Contains(
            "requires_human_visual_review=true",
            excluded.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SemanticIdentityDoesNotCollapseGenericNamesOrPriceTiers()
    {
        static InventoryExtractedRow Row(
            string locator,
            params (string Key, string Value)[] values) =>
            new(1, locator, values.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.Ordinal));

        var first = Row(
            "pptx:slide=1",
            ("name", "Billboard"),
            ("address", "1 Main Road"),
            ("rate", "1000"));
        var otherSite = Row(
            "pptx:slide=2",
            ("name", "Billboard"),
            ("address", "2 Main Road"),
            ("rate", "1000"));
        var otherPrice = Row(
            "pptx:slide=3",
            ("name", "Billboard"),
            ("address", "1 Main Road"),
            ("rate", "2000"));
        var sameProduct = Row(
            "pptx:slide=4",
            ("productcode", "SITE-001"),
            ("name", "Billboard"));

        Assert.False(InventorySemanticMerger.SameIdentity(
            first, otherSite));
        Assert.False(InventorySemanticMerger.SameIdentity(
            first, otherPrice));
        Assert.True(InventorySemanticMerger.SameIdentity(
            Row("pptx:slide=5",
                ("productcode", "SITE-001"),
                ("name", "Billboard")),
            sameProduct));
    }

    private static byte[] Spreadsheet() => CreatePackage(
        new Dictionary<string, string>
        {
            ["xl/workbook.xml"] = """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Rates" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """,
            ["xl/_rels/workbook.xml.rels"] = """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1"
                    Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"
                    Target="worksheets/sheet1.xml"/>
                </Relationships>
                """,
            ["xl/sharedStrings.xml"] = """
                <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <si><t>ALG-001</t></si>
                </sst>
                """,
            ["xl/worksheets/sheet1.xml"] = """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    <row r="1">
                      <c r="A1" t="inlineStr"><is><t>Product Code</t></is></c>
                      <c r="B1" t="inlineStr"><is><t>Name</t></is></c>
                      <c r="C1" t="inlineStr"><is><t>Rate</t></is></c>
                      <c r="D1" t="inlineStr"><is><t>Currency</t></is></c>
                      <c r="E1" t="inlineStr"><is><t>Valid From</t></is></c>
                    </row>
                    <row r="2">
                      <c r="A2" t="s"><v>0</v></c>
                      <c r="B2" t="inlineStr"><is><t>Drive Time</t></is></c>
                      <c r="C2"><f>100000+57500</f><v>157500</v></c>
                      <c r="D2" t="inlineStr"><is><t>ZAR</t></is></c>
                      <c r="E2" t="d"><v>2026-09-01T00:00:00Z</v></c>
                    </row>
                  </sheetData>
                </worksheet>
                """,
        });

    private static byte[] PresentationDeck() => CreatePackage(
        new Dictionary<string, string>
        {
            ["ppt/presentation.xml"] = """
                <p:presentation
                    xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                    xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <p:sldIdLst><p:sldId id="256" r:id="rId1"/></p:sldIdLst>
                </p:presentation>
                """,
            ["ppt/_rels/presentation.xml.rels"] = """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1"
                    Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide"
                    Target="slides/slide1.xml"/>
                </Relationships>
                """,
            ["ppt/slides/slide1.xml"] = """
                <p:sld
                    xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                    xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
                  <p:cSld><p:spTree>
                    <p:sp>
                      <p:nvSpPr><p:nvPr><p:ph type="title"/></p:nvPr></p:nvSpPr>
                      <p:txBody><a:p><a:r><a:t>ALGOA CLUB - PLAN A PACKAGE</a:t></a:r></a:p></p:txBody>
                    </p:sp>
                    <p:graphicFrame><a:graphic><a:graphicData><a:tbl>
                      <a:tr>
                        <a:tc><a:txBody><a:p><a:r><a:t>Element</a:t></a:r></a:p></a:txBody></a:tc>
                        <a:tc><a:txBody><a:p><a:r><a:t>Exposure</a:t></a:r></a:p></a:txBody></a:tc>
                        <a:tc><a:txBody><a:p><a:r><a:t>Value</a:t></a:r></a:p></a:txBody></a:tc>
                      </a:tr>
                      <a:tr>
                        <a:tc><a:txBody><a:p><a:r><a:t>Generic 30 second commercial</a:t></a:r></a:p></a:txBody></a:tc>
                        <a:tc><a:txBody><a:p><a:r><a:t>114</a:t></a:r></a:p></a:txBody></a:tc>
                        <a:tc><a:txBody><a:p><a:r><a:t>R291,060</a:t></a:r></a:p></a:txBody></a:tc>
                      </a:tr>
                      <a:tr>
                        <a:tc><a:txBody><a:p><a:r><a:t>Report sponsorship</a:t></a:r></a:p></a:txBody></a:tc>
                        <a:tc><a:txBody><a:p><a:r><a:t>30</a:t></a:r></a:p></a:txBody></a:tc>
                        <a:tc><a:txBody><a:p><a:r><a:t>R157,500</a:t></a:r></a:p></a:txBody></a:tc>
                      </a:tr>
                    </a:tbl></a:graphicData></a:graphic></p:graphicFrame>
                  </p:spTree></p:cSld>
                </p:sld>
                """,
        });

    private static byte[] ImageOnlySpreadsheet(int imageCount)
    {
        var anchors = string.Concat(
            Enumerable.Range(1, imageCount).Select(index =>
                $"<xdr:oneCellAnchor><xdr:from><xdr:col>0</xdr:col>" +
                $"<xdr:row>{index - 1}</xdr:row></xdr:from><xdr:pic>" +
                $"<xdr:blipFill><a:blip r:embed=\"rId{index}\"/>" +
                "</xdr:blipFill></xdr:pic></xdr:oneCellAnchor>"));
        var relationships = string.Concat(
            Enumerable.Range(1, imageCount).Select(index =>
                $"<Relationship Id=\"rId{index}\" " +
                "Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" " +
                $"Target=\"../media/image{index}.png\"/>"));
        return CreatePackage(
            new Dictionary<string, string>
            {
                ["xl/workbook.xml"] = """
                    <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                              xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                      <sheets><sheet name="Rates" sheetId="1" r:id="rId1"/></sheets>
                    </workbook>
                    """,
                ["xl/_rels/workbook.xml.rels"] = """
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Id="rId1" Target="worksheets/sheet1.xml"/>
                    </Relationships>
                    """,
                ["xl/worksheets/sheet1.xml"] = """
                    <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                               xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                      <sheetData/><drawing r:id="rId1"/>
                    </worksheet>
                    """,
                ["xl/worksheets/_rels/sheet1.xml.rels"] = """
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Id="rId1" Target="../drawings/drawing1.xml"/>
                    </Relationships>
                    """,
                ["xl/drawings/drawing1.xml"] =
                    "<xdr:wsDr xmlns:xdr=\"http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing\" " +
                    "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" " +
                    "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                    anchors + "</xdr:wsDr>",
                ["xl/drawings/_rels/drawing1.xml.rels"] =
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    relationships + "</Relationships>",
            }, imageCount);
    }
}
