using System.Globalization;
using System.Xml.Linq;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class NativeOfficeImageLocatorReader
{
    private const string PresentationPart =
        "ppt/presentation.xml";
    private const string WorkbookPart = "xl/workbook.xml";
    private static readonly XNamespace Presentation =
        "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace Spreadsheet =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Drawing =
        "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace SpreadsheetDrawing =
        "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
    private static readonly XNamespace Relationships =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    internal static IReadOnlyDictionary<string, string> Read(
        OpenXmlInventoryPackage package,
        string documentClass,
        IReadOnlyList<string> parts)
    {
        var result = parts.ToDictionary(
            part => part,
            part => Fallback(documentClass, part),
            StringComparer.OrdinalIgnoreCase);
        var located = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        if (documentClass ==
            MasterDataCodes.DocumentClasses.Pptx)
        {
            AddPresentationLocators(
                package, result, located);
        }
        else if (documentClass ==
                 MasterDataCodes.DocumentClasses.Xlsx)
        {
            AddSpreadsheetLocators(
                package, result, located);
        }
        return result;
    }

    private static void AddPresentationLocators(
        OpenXmlInventoryPackage package,
        IDictionary<string, string> result,
        ISet<string> located)
    {
        var presentation = package.ReadOptional(
            PresentationPart);
        if (presentation is null)
            return;
        var slide = 0;
        foreach (var slideId in presentation.Descendants(
                     Presentation + "sldId"))
        {
            slide++;
            var id = slideId.Attribute(
                Relationships + "id")?.Value;
            if (string.IsNullOrWhiteSpace(id))
                throw new InventoryExtractionUnavailableException();
            var slidePart = package.RelationshipTarget(
                PresentationPart, id);
            AddSlideLocators(
                package, slidePart, slide,
                result, located);
        }
    }

    private static void AddSlideLocators(
        OpenXmlInventoryPackage package,
        string slidePart,
        int slide,
        IDictionary<string, string> result,
        ISet<string> located)
    {
        var document = package.ReadRequired(slidePart);
        var targets = package.RelationshipTargets(
            slidePart);
        var image = 0;
        foreach (var blip in document.Descendants(
                     Drawing + "blip"))
        {
            var id = blip.Attribute(
                Relationships + "embed")?.Value;
            if (id is null ||
                !targets.TryGetValue(id, out var target) ||
                !result.ContainsKey(target))
                continue;
            image++;
            if (!located.Add(target))
                continue;
            result[target] =
                "pptx:slide=" + Number(slide) +
                ";image=" + Number(image) +
                ";embedded-part=" +
                Uri.EscapeDataString(target);
        }
    }

    private static void AddSpreadsheetLocators(
        OpenXmlInventoryPackage package,
        IDictionary<string, string> result,
        ISet<string> located)
    {
        var workbook = package.ReadOptional(WorkbookPart);
        if (workbook is null)
            return;
        var sheetNumber = 0;
        foreach (var sheet in workbook.Descendants(
                     Spreadsheet + "sheet"))
        {
            sheetNumber++;
            var name = sheet.Attribute("name")?.Value ??
                "Sheet " + Number(sheetNumber);
            var id = sheet.Attribute(
                Relationships + "id")?.Value;
            if (string.IsNullOrWhiteSpace(id))
                throw new InventoryExtractionUnavailableException();
            var sheetPart = package.RelationshipTarget(
                WorkbookPart, id);
            AddSheetLocators(
                package, sheetPart, name,
                result, located);
        }
    }

    private static void AddSheetLocators(
        OpenXmlInventoryPackage package,
        string sheetPart,
        string sheetName,
        IDictionary<string, string> result,
        ISet<string> located)
    {
        var sheet = package.ReadRequired(sheetPart);
        var targets = package.RelationshipTargets(
            sheetPart);
        foreach (var drawing in sheet.Descendants(
                     Spreadsheet + "drawing"))
        {
            var id = drawing.Attribute(
                Relationships + "id")?.Value;
            if (id is null ||
                !targets.TryGetValue(id, out var drawingPart))
                continue;
            AddDrawingLocators(
                package, drawingPart, sheetName,
                result, located);
        }
    }

    private static void AddDrawingLocators(
        OpenXmlInventoryPackage package,
        string drawingPart,
        string sheetName,
        IDictionary<string, string> result,
        ISet<string> located)
    {
        var drawing = package.ReadRequired(drawingPart);
        var targets = package.RelationshipTargets(
            drawingPart);
        var image = 0;
        foreach (var anchor in drawing.Root?.Elements()
                 ?? [])
        {
            var blip = anchor.Descendants(
                    Drawing + "blip")
                .FirstOrDefault();
            var id = blip?.Attribute(
                Relationships + "embed")?.Value;
            if (id is null ||
                !targets.TryGetValue(id, out var target) ||
                !result.ContainsKey(target))
                continue;
            image++;
            if (!located.Add(target))
                continue;
            result[target] =
                "xlsx:sheet=" +
                Uri.EscapeDataString(sheetName.Trim()) +
                ";image=" + Number(image) +
                Cell(anchor) +
                ";embedded-part=" +
                Uri.EscapeDataString(target);
        }
    }

    private static string Cell(XElement anchor)
    {
        var from = anchor.Element(
            SpreadsheetDrawing + "from");
        var row = ReadIndex(
            from?.Element(SpreadsheetDrawing + "row"));
        var column = ReadIndex(
            from?.Element(SpreadsheetDrawing + "col"));
        return row < 0 || column < 0
            ? string.Empty
            : ";cell=" + ColumnName(column + 1) +
              Number(row + 1);
    }

    private static int ReadIndex(XElement? value) =>
        int.TryParse(
            value?.Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : -1;

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

    private static string Fallback(
        string documentClass,
        string part) =>
        documentClass.ToLowerInvariant() +
        ":package;embedded-part=" +
        Uri.EscapeDataString(part);

    private static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);
}
