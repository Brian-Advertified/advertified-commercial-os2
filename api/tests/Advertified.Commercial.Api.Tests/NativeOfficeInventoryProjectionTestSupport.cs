using System.IO.Compression;
using System.Text;
using Advertified.Commercial.Infrastructure.Inventory;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DoclingInventoryExtractionAdapterTests
{
    private static byte[] MixedImageSpreadsheet() => CreatePackage(
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
            ["xl/drawings/drawing1.xml"] = """
                <xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing"
                          xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <xdr:oneCellAnchor><xdr:from><xdr:col>0</xdr:col><xdr:row>0</xdr:row></xdr:from><xdr:pic><xdr:blipFill><a:blip r:embed="rId1"/></xdr:blipFill></xdr:pic></xdr:oneCellAnchor>
                  <xdr:oneCellAnchor><xdr:from><xdr:col>1</xdr:col><xdr:row>0</xdr:row></xdr:from><xdr:pic><xdr:blipFill><a:blip r:embed="rId2"/></xdr:blipFill></xdr:pic></xdr:oneCellAnchor>
                </xdr:wsDr>
                """,
            ["xl/drawings/_rels/drawing1.xml.rels"] = """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Target="../media/image1.png"/>
                  <Relationship Id="rId2" Target="../media/vector.svg"/>
                </Relationships>
                """,
            ["xl/media/vector.svg"] =
                "<svg xmlns=\"http://www.w3.org/2000/svg\"/>",
        },
        imageCount: 1);

    private static InventoryCodeSets EmptyCodes()
    {
        var empty = new HashSet<string>(StringComparer.Ordinal);
        return new(
            empty, empty, empty, empty, empty,
            empty, empty, empty, empty);
    }

    private static byte[] CreatePackage(
        IReadOnlyDictionary<string, string> parts,
        int imageCount = 0)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(
                   output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var part in parts)
            {
                var entry = archive.CreateEntry(
                    part.Key, CompressionLevel.Fastest);
                using var stream = entry.Open();
                using var writer = new StreamWriter(
                    stream, new UTF8Encoding(false));
                writer.Write(part.Value);
            }
            var png = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwC" +
                "AAAAC0lEQVR42mP8/x8AAusB9Wl2n+0AAAAASUVORK5CYII=");
            for (var index = 1; index <= imageCount; index++)
            {
                var entry = archive.CreateEntry(
                    $"xl/media/image{index}.png",
                    CompressionLevel.Fastest);
                using var stream = entry.Open();
                stream.Write(png);
            }
        }
        return output.ToArray();
    }
}
