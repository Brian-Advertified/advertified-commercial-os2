using System.IO.Compression;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class NativeInventorySourcePreprocessorTests
{
    [Fact]
    public void CsvProducesGroundedTranscriptionPacket()
    {
        var extraction = Process(
            "rates.csv", "text/csv", MasterDataCodes.DocumentClasses.Csv,
            Encoding.UTF8.GetBytes("site,rate\nAlpha,1250"));

        var packets = InventorySemanticPacketBuilder.BuildTranscription(
            extraction, Codes(), Settings());

        var elements = Assert.IsAssignableFrom<IReadOnlyList<InventoryExtractedSourceElement>>(
            extraction.Document.SourceElements);
        Assert.Equal(4, elements.Count);
        Assert.Equal("csv:row=2;column=1", elements[2].Locator);
        Assert.Equal("Alpha", elements[2].RawValue);
        Assert.Equal("csv:row=2;column=2", elements[3].Locator);
        Assert.Equal("1250", elements[3].RawValue);
        Assert.Single(packets);
        Assert.Equal(InventorySemanticOperations.SourceTranscription,
            packets[0].Operation);
        Assert.Contains("Alpha", packets[0].RequestJson, StringComparison.Ordinal);
        Assert.Contains("1250", packets[0].RequestJson, StringComparison.Ordinal);
    }

    [Fact]
    public void DocxPreservesParagraphText()
    {
        var bytes = Package(("word/document.xml", """
            <w:document xmlns:w="urn:w"><w:body><w:p><w:r><w:t>Airport billboard</w:t></w:r></w:p></w:body></w:document>
            """));

        var result = Process("rates.docx", "application/docx",
            MasterDataCodes.DocumentClasses.Docx, bytes);

        Assert.Equal("Airport billboard",
            Assert.Single(result.Document.SourceElements!).RawValue);
    }

    [Fact]
    public void XlsxPreservesCellCoordinatesAndValues()
    {
        var bytes = Package(
            ("xl/workbook.xml", "<workbook/>"),
            ("xl/sharedStrings.xml", "<sst><si><t>Johannesburg</t></si></sst>"),
            ("xl/worksheets/sheet1.xml", "<worksheet><sheetData><row r=\"7\"><c r=\"B7\" t=\"s\"><v>0</v></c><c r=\"C7\"><v>900</v></c></row></sheetData></worksheet>"));

        var result = Process("rates.xlsx", "application/xlsx",
            MasterDataCodes.DocumentClasses.Xlsx, bytes);

        var elements = Assert.IsAssignableFrom<IReadOnlyList<InventoryExtractedSourceElement>>(
            result.Document.SourceElements);
        Assert.Equal(2, elements.Count);
        Assert.Equal("xlsx:sheet=1;cell=B7", elements[0].Locator);
        Assert.Equal(2, elements[0].Column);
        Assert.Equal("Johannesburg", elements[0].RawValue);
        Assert.Equal("xlsx:sheet=1;cell=C7", elements[1].Locator);
        Assert.Equal(3, elements[1].Column);
        Assert.Equal("900", elements[1].RawValue);
    }

    [Fact]
    public void PptxPreservesSlideTextAndRelationshipBoundImage()
    {
        var image = new byte[] { 0x89, 0x50, 0x4e, 0x47, 1, 2, 3 };
        var bytes = Package(
            ("ppt/presentation.xml", "<presentation/>"),
            ("ppt/slides/slide1.xml", "<p:sld xmlns:p=\"urn:p\" xmlns:a=\"urn:a\" xmlns:r=\"urn:r\"><a:p><a:r><a:t>Premium screen</a:t></a:r></a:p><a:blip r:embed=\"rId1\"/></p:sld>"),
            ("ppt/slides/_rels/slide1.xml.rels", "<Relationships><Relationship Id=\"rId1\" Target=\"../media/image1.png\"/></Relationships>"),
            ("ppt/media/image1.png", image));

        var result = Process("rates.pptx", "application/pptx",
            MasterDataCodes.DocumentClasses.Pptx, bytes);

        Assert.Equal("Premium screen",
            Assert.Single(result.Document.SourceElements!).RawValue);
        var extracted = Assert.Single(result.Document.SourceImages!);
        Assert.Equal("pptx:slide=1;image=1", extracted.Locator);
        Assert.Equal(Convert.ToBase64String(image), extracted.Base64Content);
    }

    [Fact]
    public void PdfPreservesPageText()
    {
        var result = Process("rates.pdf", "application/pdf",
            MasterDataCodes.DocumentClasses.Pdf, MinimalPdf("Cape Town gantry"));

        var element = Assert.Single(result.Document.SourceElements!);
        Assert.Equal("pdf:page=1", element.Locator);
        Assert.Contains("Cape Town gantry", element.RawValue,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedOpenXmlFailsClosed()
    {
        Assert.ThrowsAny<Exception>(() => Process(
            "rates.xlsx", "application/xlsx",
            MasterDataCodes.DocumentClasses.Xlsx,
            Encoding.UTF8.GetBytes("not-a-zip")));
    }

    private static InventoryExtractionResult Process(
        string fileName, string mediaType, string documentClass, byte[] bytes)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return NativeInventorySourcePreprocessor.Process(new(
            fileName, mediaType, documentClass, hash, bytes));
    }

    private static InventorySemanticOptions Settings() => new()
    {
        Enabled = true,
        ModelId = "amazon.nova-lite-v1:0",
        InputPricePerMillionTokensUsdMicros = 60_000,
        OutputPricePerMillionTokensUsdMicros = 240_000,
        PerCallCostCapUsdMicros = 100_000,
        CertificationBudgetUsdMicros = 5_000_000,
        BudgetScope = "inventory-monthly",
    };

    private static InventoryCodeSets Codes()
    {
        IReadOnlySet<string> Empty() => new HashSet<string>();
        return new(Empty(), Empty(), Empty(), Empty(), Empty(), Empty(),
            Empty(), Empty(), Empty());
    }

    private static byte[] Package(
        params (string Name, object Content)[] entries)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(
            output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = archive.CreateEntry(name).Open();
                var bytes = content is byte[] value
                    ? value
                    : Encoding.UTF8.GetBytes((string)content);
                stream.Write(bytes);
            }
        }
        return output.ToArray();
    }

    private static byte[] MinimalPdf(string text)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {text.Length + 26} >>\nstream\nBT /F1 12 Tf 72 720 Td ({text}) Tj ET\nendstream",
        };
        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(index + 1).Append(" 0 obj\n")
                .Append(objects[index]).Append("\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            builder.Append(offset.ToString(
                "D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        builder.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n")
            .Append(xref).Append("\n%%EOF");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
