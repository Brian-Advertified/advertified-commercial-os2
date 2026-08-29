using System.IO.Compression;
using System.Xml.Linq;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class XlsxInventoryExtractor
{
    internal static IReadOnlyList<InventoryTableRow> Extract(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var shared = ReadSharedStrings(archive);
        var sheetEntry = archive.Entries
            .Where(entry => entry.FullName.StartsWith(
                "xl/worksheets/sheet", StringComparison.Ordinal))
            .OrderBy(entry => entry.FullName, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new ArgumentException("The workbook has no worksheet.");
        using var sheetStream = sheetEntry.Open();
        var sheet = XDocument.Load(sheetStream);
        var table = sheet.Descendants()
            .Where(element => element.Name.LocalName == "row")
            .Select(row => ReadRow(row, shared))
            .Where(row => row.Length > 0)
            .ToArray();
        return InventorySourceExtractor.FromTable(table, "xlsx:sheet1");
    }

    private static string[] ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "si")
            .Select(item => string.Concat(item.Descendants()
                .Where(element => element.Name.LocalName == "t")
                .Select(element => element.Value)))
            .ToArray();
    }

    private static string[] ReadRow(XElement row, string[] shared)
    {
        var values = new SortedDictionary<int, string>();
        foreach (var cell in row.Elements().Where(element => element.Name.LocalName == "c"))
        {
            var reference = cell.Attribute("r")?.Value ?? string.Empty;
            var column = ColumnIndex(reference);
            var type = cell.Attribute("t")?.Value;
            var raw = cell.Elements().FirstOrDefault(element => element.Name.LocalName == "v")?.Value;
            var inline = string.Concat(cell.Descendants()
                .Where(element => element.Name.LocalName == "t")
                .Select(element => element.Value));
            values[column] = type == "s" && int.TryParse(raw, out var index) && index < shared.Length
                ? shared[index]
                : raw ?? inline;
        }
        if (values.Count == 0)
        {
            return [];
        }
        var result = new string[values.Keys.Max() + 1];
        foreach (var value in values)
        {
            result[value.Key] = value.Value;
        }
        return result;
    }

    private static int ColumnIndex(string reference)
    {
        var result = 0;
        foreach (var character in reference.TakeWhile(char.IsLetter))
        {
            result = checked(result * 26 + char.ToUpperInvariant(character) - 'A' + 1);
        }
        return Math.Max(0, result - 1);
    }
}
