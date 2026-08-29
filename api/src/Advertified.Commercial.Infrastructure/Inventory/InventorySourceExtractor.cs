using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using UglyToad.PdfPig;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventorySourceExtractor
{
    internal static IReadOnlyList<InventoryExtractedRow> Extract(
        string documentClass,
        byte[] content)
    {
        return documentClass switch
        {
            MasterDataCodes.DocumentClasses.Csv => FromTable(
                DelimitedTableParser.Parse(Encoding.UTF8.GetString(content)), "csv"),
            MasterDataCodes.DocumentClasses.Xlsx => XlsxInventoryExtractor.Extract(content),
            MasterDataCodes.DocumentClasses.Docx => DocxInventoryExtractor.Extract(content),
            MasterDataCodes.DocumentClasses.Pdf => ExtractPdf(content),
            MasterDataCodes.DocumentClasses.Png or MasterDataCodes.DocumentClasses.Jpeg =>
                [new InventoryExtractedRow(1, "image#1", new Dictionary<string, string>())],
            _ => throw new ArgumentException("The inventory document class is unsupported."),
        };
    }

    internal static IReadOnlyList<InventoryExtractedRow> FromTable(
        IReadOnlyList<IReadOnlyList<string>> table,
        string locatorPrefix)
    {
        if (table.Count == 0)
        {
            return [];
        }
        var headers = table[0].Select(NormalizeHeader).ToArray();
        var rows = new List<InventoryExtractedRow>();
        for (var index = 1; index < table.Count; index++)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var column = 0; column < headers.Length; column++)
            {
                var value = column < table[index].Count ? table[index][column].Trim() : string.Empty;
                if (headers[column].Length > 0 && value.Length > 0)
                {
                    values[headers[column]] = value;
                }
            }
            if (values.Count > 0)
            {
                rows.Add(new InventoryExtractedRow(index, $"{locatorPrefix}#row={index + 1}", values));
            }
        }
        return rows;
    }

    internal static string NormalizeHeader(string value) => new(
        value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static IReadOnlyList<InventoryExtractedRow> ExtractPdf(byte[] content)
    {
        using var document = PdfDocument.Open(content);
        var lines = document.GetPages()
            .Select(page => page.Text.Replace(';', '\n'))
            .SelectMany(text => text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .ToArray();
        var table = DelimitedTableParser.Parse(string.Join('\n', lines));
        if (table.Count > 1 && table[0].Count > 1)
        {
            return FromTable(table, "pdf");
        }
        return FromKeyValueLines(lines, "pdf");
    }

    internal static IReadOnlyList<InventoryExtractedRow> FromKeyValueLines(
        IReadOnlyList<string> lines,
        string locatorPrefix)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var separator = line.IndexOf(':');
            if (separator > 0 && separator < line.Length - 1)
            {
                values[NormalizeHeader(line[..separator])] = line[(separator + 1)..].Trim();
            }
        }
        return [new InventoryExtractedRow(1, $"{locatorPrefix}#record=1", values)];
    }
}

internal static class DocxInventoryExtractor
{
    internal static IReadOnlyList<InventoryExtractedRow> Extract(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry("word/document.xml")
            ?? throw new ArgumentException("The Word document has no document body.");
        using var body = entry.Open();
        var document = XDocument.Load(body);
        var table = document.Descendants()
            .Where(node => node.Name.LocalName == "tr")
            .Select(row => (IReadOnlyList<string>)row.Elements()
                .Where(cell => cell.Name.LocalName == "tc")
                .Select(CellText).ToArray())
            .Where(row => row.Count > 0).ToArray();
        if (table.Length > 1)
        {
            return InventorySourceExtractor.FromTable(table, "docx");
        }
        var lines = document.Descendants()
            .Where(node => node.Name.LocalName == "p")
            .Select(CellText).Where(value => value.Length > 0).ToArray();
        return InventorySourceExtractor.FromKeyValueLines(lines, "docx");
    }

    private static string CellText(XElement element) => string.Concat(
        element.Descendants().Where(node => node.Name.LocalName == "t")
            .Select(node => node.Value)).Trim();
}
