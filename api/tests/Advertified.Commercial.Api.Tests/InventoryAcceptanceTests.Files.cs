using System.IO.Compression;
using System.Text;
using System.Globalization;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class InventoryAcceptanceTests
{
    private sealed record FileFixture(
        string DocumentClass,
        string FileName,
        string MediaType,
        byte[] Content);

    private static FileFixture CsvFixture(string code = "OOH-001") => new(
        "CSV", "held-out-sites.csv", "text/csv", Encoding.UTF8.GetBytes(
            "product_code,name,channel,geography,latitude,longitude,rate_type,currency," +
            $"rate_minor,availability\n{code},Bree Street Gantry,OOH,Johannesburg," +
            "-26.2041,28.0473,MONTH_RATE,ZAR,125000,UNKNOWN\n"));

    private static IReadOnlyList<FileFixture> CorpusFixtures() =>
    [
        CsvFixture("CSV-001"),
        new("XLSX", "held-out-radio.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            BuildXlsx()),
        new("DOCX", "held-out-radio.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            BuildDocx()),
        new("PDF", "held-out-radio.pdf", "application/pdf", BuildPdf()),
        new("PNG", "held-out-site.png", "image/png",
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00]),
        new("JPEG", "held-out-site.jpg", "image/jpeg", [0xff, 0xd8, 0xff, 0xd9]),
    ];

    private static byte[] BuildXlsx()
    {
        using var result = new MemoryStream();
        using (var archive = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddZipText(archive, "xl/workbook.xml", "<workbook/>");
            AddZipText(archive, "xl/worksheets/sheet1.xml", """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    <row r="1"><c r="A1" t="inlineStr"><is><t>product_code</t></is></c><c r="B1" t="inlineStr"><is><t>name</t></is></c><c r="C1" t="inlineStr"><is><t>channel</t></is></c><c r="D1" t="inlineStr"><is><t>geography</t></is></c><c r="E1" t="inlineStr"><is><t>rate_type</t></is></c><c r="F1" t="inlineStr"><is><t>currency</t></is></c><c r="G1" t="inlineStr"><is><t>rate_minor</t></is></c></row>
                    <row r="2"><c r="A2" t="inlineStr"><is><t>RAD-XLSX</t></is></c><c r="B2" t="inlineStr"><is><t>Metro FM</t></is></c><c r="C2" t="inlineStr"><is><t>RADIO</t></is></c><c r="D2" t="inlineStr"><is><t>Gauteng</t></is></c><c r="E2" t="inlineStr"><is><t>SPOT_RATE</t></is></c><c r="F2" t="inlineStr"><is><t>ZAR</t></is></c><c r="G2"><v>25000</v></c></row>
                  </sheetData>
                </worksheet>
                """);
        }
        return result.ToArray();
    }

    private static byte[] BuildDocx()
    {
        using var result = new MemoryStream();
        using (var archive = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddZipText(archive, "word/document.xml", """
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:tbl>
                <w:tr><w:tc><w:p><w:r><w:t>product_code</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>name</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>channel</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>geography</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>rate_type</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>currency</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>rate_minor</w:t></w:r></w:p></w:tc></w:tr>
                <w:tr><w:tc><w:p><w:r><w:t>RAD-DOCX</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>702</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>RADIO</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Gauteng</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>SPOT_RATE</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>ZAR</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>20000</w:t></w:r></w:p></w:tc></w:tr>
                </w:tbl></w:body></w:document>
                """);
        }
        return result.ToArray();
    }

    private static byte[] BuildPdf()
    {
        const string content = "BT /F1 10 Tf 40 750 Td (product_code: RAD-PDF; name: Cape Talk; channel: RADIO; geography: Western Cape; rate_type: SPOT_RATE; currency: ZAR; rate_minor: 22000) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
        };
        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            builder.Append(offset.ToString("D10", CultureInfo.InvariantCulture))
                .Append(" 00000 n \n");
        }
        builder.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n")
            .Append(xref).Append("\n%%EOF");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static void AddZipText(ZipArchive archive, string path, string content)
    {
        using var writer = new StreamWriter(
            archive.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
