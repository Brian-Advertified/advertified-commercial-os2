using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DoclingInventoryExtractionAdapterTests
{
    [Fact]
    public void NativePresentationSiteSlideCreatesOneSourceGroundedOohCandidate()
    {
        var content = CreatePackage(new Dictionary<string, string>
        {
            ["ppt/presentation.xml"] = """
                <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                    xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <p:sldIdLst><p:sldId id="256" r:id="rId1"/></p:sldIdLst>
                </p:presentation>
                """,
            ["ppt/_rels/presentation.xml.rels"] = """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Target="slides/slide1.xml"/>
                </Relationships>
                """,
            ["ppt/slides/slide1.xml"] = Slide(
                "YJB 108A",
                "N1 Highway: William Nicol to Rivonia Road",
                "5m x 12m",
                "Bryanston",
                "Johannesburg",
                "Gauteng",
                "Immediate",
                "Digital Screen",
                "-26.034369, 28.037975",
                "TBC"),
        });
        var request = new InventoryExtractionRequest(
            "Summit OOH Media - Digital Billboard Network - 2025.pptx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            MasterDataCodes.DocumentClasses.Pptx,
            new string('d', 64),
            content);

        var rows = NativePresentationProjection.Read(request);
        var row = Assert.Single(rows);
        var candidate = InventoryCandidateNormalizer.Normalize(
            row,
            request.SourceHash,
            DateTimeOffset.UnixEpoch);

        Assert.Equal("YJB 108A", candidate.Values.ProductCode);
        Assert.Contains(
            "William Nicol",
            candidate.Values.Name,
            StringComparison.Ordinal);
        Assert.Equal("5 x 12", candidate.Values.Deliverable!.Dimensions);
        Assert.Null(candidate.Values.Geography); // Unlabelled place names require schema interpretation.
        Assert.Equal(-26.034369m, candidate.Values.Latitude);
        Assert.Equal(28.037975m, candidate.Values.Longitude);
        Assert.Equal(
            MasterDataCodes.AvailabilityStatuses.Available,
            candidate.Values.Availability);
        Assert.Null(candidate.Values.RateAmountMinor);
        Assert.Null(candidate.Values.RateType);
    }

    [Fact]
    public void ReveelRouteNumberIsNotAPriceAndPortfolioHeadingIsNotInventory()
    {
        var content = CreatePackage(new Dictionary<string, string>
        {
            ["ppt/presentation.xml"] = """
                <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                    xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <p:sldIdLst><p:sldId id="256" r:id="rId1"/></p:sldIdLst>
                </p:presentation>
                """,
            ["ppt/_rels/presentation.xml.rels"] = """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Target="slides/slide1.xml"/>
                </Relationships>
                """,
            ["ppt/slides/slide1.xml"] = Slide(
                "CAPE TOWN MARKET DIGITAL NETWORK\nLifestyle Markets",
                "JOHANNESBURG\nR24 / N1 2 Interchange"),
        });
        var request = new InventoryExtractionRequest(
            "Reveel - ZA - Publisher Media Kit.pptx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            MasterDataCodes.DocumentClasses.Pptx,
            new string('e', 64),
            content);

        var row = Assert.Single(NativePresentationProjection.Read(request));
        var candidate = InventoryCandidateNormalizer.Normalize(
            row,
            request.SourceHash,
            DateTimeOffset.UnixEpoch);

        Assert.Equal(
            "JOHANNESBURG - R24 / N1 2 Interchange",
            candidate.Values.Name);
        Assert.Null(candidate.Values.RateAmountMinor);
        Assert.Null(candidate.Values.Currency);
        Assert.DoesNotContain(
            "MARKET DIGITAL NETWORK",
            candidate.Values.Name,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RsdRouteLocationDoesNotBecomeACommercialRate()
    {
        var content = CreatePackage(new Dictionary<string, string>
        {
            ["ppt/presentation.xml"] = """
                <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                    xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <p:sldIdLst><p:sldId id="256" r:id="rId1"/></p:sldIdLst>
                </p:presentation>
                """,
            ["ppt/_rels/presentation.xml.rels"] = """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Target="slides/slide1.xml"/>
                </Relationships>
                """,
            ["ppt/slides/slide1.xml"] = Slide(
                "R24 BEDFORDVIEW",
                "TOGD007"),
        });
        var request = new InventoryExtractionRequest(
            "RSD Rate Cards - Gauteng - 2025.pptx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            MasterDataCodes.DocumentClasses.Pptx,
            new string('9', 64),
            content);

        var row = Assert.Single(NativePresentationProjection.Read(request));
        var candidate = InventoryCandidateNormalizer.Normalize(
            row,
            request.SourceHash,
            DateTimeOffset.UnixEpoch);

        Assert.Equal("TOGD007", candidate.Values.ProductCode);
        Assert.Null(candidate.Values.RateAmountMinor);
        Assert.Null(candidate.Values.Currency);
    }

    [Fact]
    public void RepeatedLocationOnDifferentSlidesRemainsTwoPhysicalCandidates()
    {
        var content = CreatePackage(new Dictionary<string, string>
        {
            ["ppt/presentation.xml"] = """
                <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                    xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <p:sldIdLst>
                    <p:sldId id="256" r:id="rId1"/>
                    <p:sldId id="257" r:id="rId2"/>
                  </p:sldIdLst>
                </p:presentation>
                """,
            ["ppt/_rels/presentation.xml.rels"] = """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Target="slides/slide1.xml"/>
                  <Relationship Id="rId2" Target="slides/slide2.xml"/>
                </Relationships>
                """,
            ["ppt/slides/slide1.xml"] = Slide("SANDTON\nRivonia Road"),
            ["ppt/slides/slide2.xml"] = Slide("SANDTON\nRivonia Road"),
        });
        var request = new InventoryExtractionRequest(
            "Reveel - ZA - Publisher Media Kit.pptx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            MasterDataCodes.DocumentClasses.Pptx,
            new string('f', 64),
            content);
        var provider = InventoryExtractionContract.Create(
            "docling",
            "test",
            InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash,
            "{}",
            []);

        var result = NativeOfficeInventoryProjection.Apply(request, provider);

        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.Equal(
            "SANDTON - Rivonia Road",
            InventoryCandidateNormalizer.Normalize(
                row,
                request.SourceHash,
                DateTimeOffset.UnixEpoch).Values.Name));
        Assert.NotEqual(result.Rows[0].Locator, result.Rows[1].Locator);
    }

    private static string Slide(params string[] values)
    {
        var shapes = string.Join("", values.Select((value, index) => $"""
            <p:sp>
              <p:nvSpPr><p:cNvPr id="{index + 1}" name="Text {index + 1}"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
              <p:spPr/>
              <p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>{Escape(value)}</a:t></a:r></a:p></p:txBody>
            </p:sp>
            """));
        return $"""
            <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
              <p:cSld><p:spTree>{shapes}</p:spTree></p:cSld>
            </p:sld>
            """;
    }

    private static string Escape(string value) =>
        System.Security.SecurityElement.Escape(value) ?? string.Empty;
}
