using System.Globalization;
using System.Xml.Linq;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class NativeSpreadsheetProjection
{
    private const string WorkbookPart = "xl/workbook.xml";
    private static readonly XNamespace Spreadsheet =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    internal static IReadOnlyList<InventoryExtractedRow> Read(
        InventoryExtractionRequest request)
    {
        using var package = OpenXmlInventoryPackage.Open(request.Content);
        var workbook = package.ReadRequired(WorkbookPart);
        var sharedStrings = ReadSharedStrings(package);
        var dateStyles = ReadDateStyles(package);
        var rows = new List<InventoryExtractedRow>();
        var sheetNumber = 0;
        foreach (var sheet in workbook.Descendants(
                     Spreadsheet + "sheet"))
        {
            sheetNumber++;
            var name = (string?)sheet.Attribute("name") ??
                "Sheet " + sheetNumber.ToString(
                    CultureInfo.InvariantCulture);
            var relationshipId =
                (string?)sheet.Attribute(Relationships + "id");
            if (string.IsNullOrWhiteSpace(relationshipId))
                throw new InventoryExtractionUnavailableException();
            var part = package.RelationshipTarget(
                WorkbookPart, relationshipId);
            AddSheetRows(
                rows, package.ReadRequired(part), name,
                sharedStrings, dateStyles);
        }
        if (rows.Count == 0 &&
            package.HasPartPrefix("xl/media/"))
        {
            rows.Add(VisualReviewRow());
        }
        return Renumber(rows);
    }

    private static void AddSheetRows(
        List<InventoryExtractedRow> result,
        XDocument sheet,
        string sheetName,
        IReadOnlyList<string> sharedStrings,
        IReadOnlySet<int> dateStyles)
    {
        var cells = ReadCells(
            sheet, sharedStrings, dateStyles);
        ApplyMergedCells(sheet, cells);
        var rows = cells.Values
            .GroupBy(cell => cell.Row)
            .Select(group => new InventoryTableRow(
                group.Key,
                group.ToDictionary(
                    cell => cell.Column,
                    cell => cell.Value)))
            .OrderBy(row => row.SourceRow)
            .ToArray();
        var tableNumber = 0;
        foreach (var block in SplitBlocks(rows))
        {
            tableNumber++;
            result.AddRange(NativeOfficeTableProjection.Project(
                block,
                result.Count,
                row => SheetRowLocator(
                    sheetName, tableNumber, row),
                (row, column) => SheetCellLocator(
                    sheetName, tableNumber, row, column)));
        }
    }

    private static Dictionary<(int Row, int Column), SpreadsheetCell>
        ReadCells(
            XDocument sheet,
            IReadOnlyList<string> sharedStrings,
            IReadOnlySet<int> dateStyles)
    {
        var result =
            new Dictionary<(int Row, int Column), SpreadsheetCell>();
        foreach (var cell in sheet.Descendants(Spreadsheet + "c"))
        {
            var reference = (string?)cell.Attribute("r");
            if (!TryReference(reference, out var row, out var column))
                continue;
            var value = ReadValue(
                cell, sharedStrings, dateStyles);
            if (value.Length > 0)
                result[(row, column)] =
                    new SpreadsheetCell(row, column, value);
        }
        return result;
    }

    private static string ReadValue(
        XElement cell,
        IReadOnlyList<string> sharedStrings,
        IReadOnlySet<int> dateStyles)
    {
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr")
            return Text(cell.Element(Spreadsheet + "is"));
        var raw = cell.Element(Spreadsheet + "v")?.Value?.Trim()
            ?? string.Empty;
        if (type == "s" &&
            int.TryParse(raw, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var index) &&
            index >= 0 && index < sharedStrings.Count)
        {
            return sharedStrings[index];
        }
        if (type == "b")
            return raw == "1" ? "TRUE" : "FALSE";
        if (type == "d")
            return NormalizeDate(raw);
        var style = (int?)cell.Attribute("s");
        return style.HasValue &&
               dateStyles.Contains(style.Value)
            ? NormalizeSerialDate(raw)
            : raw;
    }

    private static string[] ReadSharedStrings(
        OpenXmlInventoryPackage package)
    {
        var document = package.ReadOptional(
            "xl/sharedStrings.xml");
        return document is null
            ? []
            : document.Descendants(Spreadsheet + "si")
                .Select(Text)
                .ToArray();
    }

    private static HashSet<int> ReadDateStyles(
        OpenXmlInventoryPackage package)
    {
        var document = package.ReadOptional("xl/styles.xml");
        if (document is null) return [];
        var custom = document.Descendants(
                Spreadsheet + "numFmt")
            .Where(item => IsDateFormat(
                (string?)item.Attribute("formatCode")))
            .Select(item => (int?)item.Attribute("numFmtId"))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToHashSet();
        var builtIn = new HashSet<int>(
            Enumerable.Range(14, 9)
                .Concat(Enumerable.Range(45, 3)));
        var result = new HashSet<int>();
        var cellXfs = document.Root?
            .Element(Spreadsheet + "cellXfs");
        if (cellXfs is null) return result;
        var index = 0;
        foreach (var style in cellXfs.Elements(
                     Spreadsheet + "xf"))
        {
            var numberFormat = (int?)style.Attribute("numFmtId")
                ?? 0;
            if (builtIn.Contains(numberFormat) ||
                custom.Contains(numberFormat))
                result.Add(index);
            index++;
        }
        return result;
    }

    private static void ApplyMergedCells(
        XDocument sheet,
        IDictionary<(int Row, int Column), SpreadsheetCell> cells)
    {
        foreach (var merged in sheet.Descendants(
                     Spreadsheet + "mergeCell"))
        {
            if (!TryRange(
                    (string?)merged.Attribute("ref"),
                    out var startRow, out var startColumn,
                    out var endRow, out var endColumn) ||
                !cells.TryGetValue(
                    (startRow, startColumn), out var source) ||
                (endRow - startRow + 1L) *
                (endColumn - startColumn + 1L) > 256)
                continue;
            for (var row = startRow; row <= endRow; row++)
            for (var column = startColumn;
                 column <= endColumn; column++)
                cells.TryAdd(
                    (row, column),
                    source with
                    {
                        Row = row,
                        Column = column,
                    });
        }
    }

    private static List<InventoryTableRow[]> SplitBlocks(
        IReadOnlyList<InventoryTableRow> rows)
    {
        var result = new List<InventoryTableRow[]>();
        var current = new List<InventoryTableRow>();
        var previous = -10;
        foreach (var row in rows)
        {
            if (current.Count > 0 &&
                row.SourceRow - previous > 2)
            {
                result.Add(current.ToArray());
                current.Clear();
            }
            current.Add(row);
            previous = row.SourceRow;
        }
        if (current.Count > 0)
            result.Add(current.ToArray());
        return result;
    }

    private static InventoryExtractedRow VisualReviewRow() => new(
        1,
        "xlsx:workbook;embedded-images",
        new SortedDictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["extractionblocker"] =
                NativeOfficeImageReader.RequiredBlocker,
        });

    private static InventoryExtractedRow[] Renumber(
        IReadOnlyList<InventoryExtractedRow> rows) =>
        rows.Select((row, index) =>
            row with { Number = index + 1 }).ToArray();

    private static string Text(XElement? element) =>
        element is null
            ? string.Empty
            : string.Concat(element.DescendantsAndSelf(
                    Spreadsheet + "t")
                .Select(item => item.Value)).Trim();

    private static string NormalizeDate(string value) =>
        DateTimeOffset.TryParse(
            value, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var parsed)
            ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : value;

    private static string NormalizeSerialDate(string value) =>
        double.TryParse(
            value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var serial) &&
        serial is >= 0 and <= 2_958_465
            ? DateTime.FromOADate(serial)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : value;

    private static bool IsDateFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var code = value.ToLowerInvariant()
            .Replace("\\", string.Empty, StringComparison.Ordinal);
        return code.Contains('y') &&
               (code.Contains('d') || code.Contains('m'));
    }

    private static bool TryRange(
        string? value,
        out int startRow,
        out int startColumn,
        out int endRow,
        out int endColumn)
    {
        startRow = startColumn = endRow = endColumn = 0;
        var parts = value?.Split(':') ?? [];
        return parts.Length == 2 &&
               TryReference(
                   parts[0], out startRow, out startColumn) &&
               TryReference(
                   parts[1], out endRow, out endColumn);
    }

    private static bool TryReference(
        string? value,
        out int row,
        out int column)
    {
        row = 0;
        column = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var index = 0;
        while (index < value.Length &&
               char.IsLetter(value[index]))
        {
            column = checked(
                column * 26 +
                char.ToUpperInvariant(value[index]) - 'A' + 1);
            index++;
        }
        return column > 0 &&
               int.TryParse(
                   value[index..],
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out row) &&
               row > 0;
    }

    private static string SheetRowLocator(
        string sheet,
        int table,
        int row) =>
        "xlsx:sheet=" + Escape(sheet) +
        ";table=" + table.ToString(CultureInfo.InvariantCulture) +
        ";row=" + row.ToString(CultureInfo.InvariantCulture);

    private static string SheetCellLocator(
        string sheet,
        int table,
        int row,
        int column) =>
        SheetRowLocator(sheet, table, row) +
        ";cell=" + ColumnName(column);

    private static string ColumnName(int column)
    {
        var value = column;
        var result = string.Empty;
        while (value > 0)
        {
            value--;
            result = (char)('A' + value % 26) + result;
            value /= 26;
        }
        return result;
    }

    private static string Escape(string value) =>
        Uri.EscapeDataString(value.Trim());

    private sealed record SpreadsheetCell(
        int Row,
        int Column,
        string Value);
}
