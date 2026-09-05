using System.Globalization;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DoclingInventoryExtractionAdapterTests
{
    private static readonly string[] DmsPhysicalNames =
    [
        "DStv Stream VOD",
        "DStv Stream VOD",
        "DStv Stream Live",
        "You Tube",
    ];

    private static readonly string[] DmsPhysicalRates =
        ["R575", "R1,10", "R500", "R200"];

    [Fact]
    public void NativeSpreadsheetPreservesDmsRowsBeforeSemanticEnrichment()
    {
        var hash = new string('e', 64);
        var request = new InventoryExtractionRequest(
            "DMS Digital Rate Card .xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            MasterDataCodes.DocumentClasses.Xlsx,
            hash,
            DmsRateCardSpreadsheet());

        var rows = NativeSpreadsheetProjection.Read(request);

        Assert.Equal(4, rows.Count);
        Assert.Equal(
            DmsPhysicalNames,
            rows.Select(row => row.Values["platform"]).ToArray());
        Assert.Equal(
            DmsPhysicalRates,
            rows.Select(row => row.Values["rate"]).ToArray());
        Assert.Equal(
            "xlsx:sheet=Rates;table=1;row=2;cell=A",
            rows[0].FieldLocators!["platform"]);
        Assert.Equal(
            "xlsx:sheet=Rates;table=1;row=2;cell=B",
            rows[0].FieldLocators!["adunit"]);

        var candidates = rows.Select(row =>
            InventoryCandidateNormalizer.Normalize(
                row,
                hash,
                DateTimeOffset.Parse(
                    "2026-09-04T00:00:00Z",
                    CultureInfo.InvariantCulture))).ToArray();

        Assert.Equal("DStv Stream VOD", candidates[0].Values.Name);
        Assert.Equal("Video Pre Roll", candidates[0].Values.Deliverable!.Placement);
        Assert.Equal(
            "MP4 Skippable after 5 Seconds",
            candidates[0].Values.Deliverable!.Format);
        Assert.Equal("ZAR", candidates[0].Values.Currency);
        Assert.Equal(57_500, candidates[0].Values.RateAmountMinor);
        Assert.Null(candidates[0].Values.RateType);
        Assert.Null(candidates[0].Values.Channel);
        Assert.Null(candidates[0].Values.ProductType);

        Assert.Equal("DStv Stream VOD", candidates[1].Values.Name);
        Assert.Equal("ZAR", candidates[1].Values.Currency);
        Assert.Null(candidates[1].Values.RateAmountMinor);
        Assert.Null(candidates[1].Values.RateType);
        Assert.Equal(
            "AMBIGUOUS_TRUNCATED_RATE",
            candidates[1].Values.Extension!["rateambiguity"]);
        Assert.DoesNotContain(
            candidates[1].Evidence,
            item => item.FieldName == "rate" &&
                item.NormalizedValue is not null);

        Assert.Equal(50_000, candidates[2].Values.RateAmountMinor);
        Assert.Equal(20_000, candidates[3].Values.RateAmountMinor);
        Assert.All(candidates, candidate =>
            Assert.Null(candidate.Values.CommercialTerms));
    }

    private static byte[] DmsRateCardSpreadsheet() => CreatePackage(
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
            ["xl/worksheets/sheet1.xml"] = """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    <row r="1">
                      <c r="A1" t="inlineStr"><is><t>Platform</t></is></c>
                      <c r="B1" t="inlineStr"><is><t>Ad Unit</t></is></c>
                      <c r="C1" t="inlineStr"><is><t>Format</t></is></c>
                      <c r="D1" t="inlineStr"><is><t>Rate</t></is></c>
                    </row>
                    <row r="2">
                      <c r="A2" t="inlineStr"><is><t>DStv Stream VOD</t></is></c>
                      <c r="B2" t="inlineStr"><is><t>Video Pre Roll</t></is></c>
                      <c r="C2" t="inlineStr"><is><t>MP4 Skippable after 5 Seconds</t></is></c>
                      <c r="D2" t="inlineStr"><is><t>R575</t></is></c>
                    </row>
                    <row r="3">
                      <c r="A3" t="inlineStr"><is><t>DStv Stream VOD</t></is></c>
                      <c r="B3" t="inlineStr"><is><t>Video Pre Roll</t></is></c>
                      <c r="C3" t="inlineStr"><is><t>MP4 15 seconds non skip</t></is></c>
                      <c r="D3" t="inlineStr"><is><t>R1,10</t></is></c>
                    </row>
                    <row r="4">
                      <c r="A4" t="inlineStr"><is><t>DStv Stream Live</t></is></c>
                      <c r="B4" t="inlineStr"><is><t>Video</t></is></c>
                      <c r="C4" t="inlineStr"><is><t>MP4</t></is></c>
                      <c r="D4" t="inlineStr"><is><t>R500</t></is></c>
                    </row>
                    <row r="5">
                      <c r="A5" t="inlineStr"><is><t>You Tube</t></is></c>
                      <c r="B5" t="inlineStr"><is><t>Video Pre Roll</t></is></c>
                      <c r="C5" t="inlineStr"><is><t>MP4</t></is></c>
                      <c r="D5" t="inlineStr"><is><t>R200</t></is></c>
                    </row>
                  </sheetData>
                </worksheet>
                """,
        });
}
