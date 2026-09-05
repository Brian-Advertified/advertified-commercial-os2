using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class NativePresentationProjection
{
    private const string PresentationPart = "ppt/presentation.xml";
    private static readonly XNamespace Presentation =
        "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace Drawing =
        "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Relationships =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    [GeneratedRegex(
        @"(?<![A-Za-z])(?:ZAR|R)\s*\d[\d\s.,\u00A0]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MoneyPattern();

    internal static IReadOnlyList<InventoryExtractedRow> Read(
        InventoryExtractionRequest request)
    {
        using var package = OpenXmlInventoryPackage.Open(request.Content);
        var presentation = package.ReadRequired(PresentationPart);
        var rows = new List<InventoryExtractedRow>();
        var slideNumber = 0;
        foreach (var slideId in presentation.Descendants(
                     Presentation + "sldId"))
        {
            slideNumber++;
            var relationshipId =
                (string?)slideId.Attribute(Relationships + "id");
            if (string.IsNullOrWhiteSpace(relationshipId))
                throw new InventoryExtractionUnavailableException();
            var part = package.RelationshipTarget(
                PresentationPart, relationshipId);
            AddSlideRows(
                rows, package.ReadRequired(part), slideNumber);
        }
        if (rows.Count == 0 &&
            package.HasPartPrefix("ppt/media/"))
            rows.Add(VisualReviewRow());
        return rows.Select((row, index) =>
            row with { Number = index + 1 }).ToArray();
    }

    private static void AddSlideRows(
        List<InventoryExtractedRow> result,
        XDocument slide,
        int slideNumber)
    {
        var title = ReadTitle(slide);
        var slideText = SlideText(slide);
        var context = new Dictionary<string, string>(
            StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(slideText))
            context["description"] =
                Limit(slideText, 2_000);
        if (IsPackageTitle(title))
            context["packagename"] =
                Limit(title, 500);
        var tableRows = new List<InventoryExtractedRow>();
        var tableNumber = 0;
        foreach (var table in slide.Descendants(
                     Drawing + "tbl"))
        {
            tableNumber++;
            var rows = ReadTable(table);
            tableRows.AddRange(NativeOfficeTableProjection.Project(
                rows,
                result.Count + tableRows.Count,
                row => TableRowLocator(
                    slideNumber, tableNumber, row),
                (row, column) => TableCellLocator(
                    slideNumber, tableNumber, row, column),
                context,
                SlideLocator(slideNumber)));
        }
        var siteRows = ReadSiteRows(slide, slideNumber);
        var mergedRows = MergeSlideRows(tableRows, siteRows);
        result.AddRange(mergedRows.Select((row, index) =>
            row with { Number = result.Count + index + 1 }));
        if (!siteRows.Any(row =>
                row.Values.ContainsKey("productcode")))
        {
            AddPricedTextRows(
                result, slide, slideNumber, title);
        }
    }

    private static InventoryExtractedRow[] MergeSlideRows(
        List<InventoryExtractedRow> tableRows,
        InventoryExtractedRow[] siteRows)
    {
        if (tableRows.Count == 1 && siteRows.Length == 1)
        {
            return
            [
                NativeOfficeInventoryProjection.MergeMissing(
                    tableRows[0], siteRows[0]),
            ];
        }
        return tableRows.Concat(siteRows).ToArray();
    }

    private static InventoryTableRow[] ReadTable(
        XElement table)
    {
        var result = new List<InventoryTableRow>();
        var rowNumber = 0;
        foreach (var row in table.Elements(Drawing + "tr"))
        {
            rowNumber++;
            var values = new Dictionary<int, string>();
            var column = 0;
            foreach (var cell in row.Elements(
                         Drawing + "tc"))
            {
                column++;
                var value = Text(cell);
                var span = Math.Max(
                    1,
                    (int?)cell.Element(Drawing + "tcPr")?
                        .Element(Drawing + "gridSpan")?
                        .Attribute("val") ?? 1);
                if (value.Length > 0)
                    for (var offset = 0;
                         offset < span; offset++)
                        values[column + offset] = value;
                column += span - 1;
            }
            if (values.Count > 0)
                result.Add(new InventoryTableRow(
                    rowNumber, values));
        }
        return result.ToArray();
    }

    private static void AddPricedTextRows(
        List<InventoryExtractedRow> result,
        XDocument slide,
        int slideNumber,
        string title)
    {
        var shapeNumber = 0;
        var previous = title;
        foreach (var shape in slide.Descendants(
                     Presentation + "sp"))
        {
            shapeNumber++;
            var paragraphNumber = 0;
            foreach (var paragraph in shape.Descendants(
                         Drawing + "p"))
            {
                paragraphNumber++;
                var text = Text(paragraph);
                if (text.Length == 0) continue;
                foreach (Match match in MoneyPattern().Matches(text))
                    TryAddPricedTextRow(
                        result, text, previous, match,
                        slideNumber, shapeNumber,
                        paragraphNumber, title);
                previous = text;
            }
        }
    }

    private static void TryAddPricedTextRow(
        List<InventoryExtractedRow> result,
        string text,
        string previous,
        Match match,
        int slideNumber,
        int shapeNumber,
        int paragraphNumber,
        string title)
    {
        if (IsRouteNumber(text, match))
            return;
        var rawMoney = match.Value.Trim()
            .TrimEnd('.', ',');
        if (!InventoryMoneyParser.TryParse(
                rawMoney, out _, out var currency) ||
            currency.Length == 0)
            return;
        var name = text[..match.Index].Trim(
            ' ', ':', '-', '–', '—');
        if (name.Length == 0)
            name = previous.Trim();
        if (name.Length == 0)
            return;
        name = Limit(name, 500);
        var locator = ParagraphLocator(
            slideNumber, shapeNumber,
            paragraphNumber, match.Index + 1);
        var isPackage =
            IsPackageTitle(title + " " + text);
        var values = new SortedDictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["currency"] = currency,
            ["description"] = Limit(text, 2_000),
            ["name"] = name,
            ["rate"] = rawMoney,
        };
        if (isPackage)
            values["ratetype"] =
                MasterDataCodes.RateTypes.PackageRate;
        result.Add(SourceLinkedOffer(
            result.Count + 1, locator, values));
    }

    private static bool IsRouteNumber(string text, Match match)
    {
        var compact = string.Concat(
            match.Value.Where(character => !char.IsWhiteSpace(character)));
        if (!Regex.IsMatch(
                compact,
                @"^R\d{1,3}$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return false;
        }
        var suffix = text[(match.Index + match.Length)..].TrimStart();
        if (Regex.IsMatch(
                suffix,
                @"^(?:/|\\|[-–—])|^(?:road|route|freeway|highway|intersection|interchange)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return true;
        }
        var preceding = text[..match.Index];
        var lineStart = Math.Max(
            preceding.LastIndexOf('\n'),
            preceding.LastIndexOf('\r')) + 1;
        if (preceding[lineStart..].Trim().Length > 0)
            return false;
        if (Regex.IsMatch(
                suffix,
                @"^(?:per|each|cpm|cpc|cpl|cpa|day|week|month|spot|unit|incl|excl|vat)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return false;
        }
        return Regex.IsMatch(
            suffix,
            @"^[A-Za-z][A-Za-z .'-]+$",
            RegexOptions.CultureInvariant);
    }

    private static InventoryExtractedRow SourceLinkedOffer(
        int number,
        string locator,
        IReadOnlyDictionary<string, string> values)
    {
        var locators = values.Keys.ToDictionary(
            key => key, _ => locator,
            StringComparer.Ordinal);
        var bases = new Dictionary<string, string>(
            StringComparer.Ordinal);
        var transformations = new Dictionary<string, string>(
            StringComparer.Ordinal);
        if (values.ContainsKey("ratetype"))
        {
            bases["ratetype"] =
                MasterDataCodes.InventoryEvidenceBases.DerivedPolicy;
            transformations["ratetype"] = MasterDataCodes
                .InventoryTransformationTypes
                .DerivedFromSourceContext;
        }
        return new InventoryExtractedRow(
            number,
            locator,
            values,
            MasterDataCodes.InventoryExtractionMethods.KeyValue,
            null,
            locators,
            null,
            bases,
            transformations);
    }

    private static string ReadTitle(XDocument slide)
    {
        foreach (var shape in slide.Descendants(
                     Presentation + "sp"))
        {
            var placeholder = shape.Descendants(
                    Presentation + "ph")
                .FirstOrDefault();
            var type = (string?)placeholder?.Attribute("type");
            if (type is "title" or "ctrTitle")
                return Text(shape);
        }
        return string.Empty;
    }

    private static string SlideText(XDocument slide) =>
        string.Join(
            "\n",
            slide.Descendants(Presentation + "sp")
                .SelectMany(shape =>
                    shape.Descendants(Drawing + "p"))
                .Select(Text)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal));

    private static string Text(XElement element) =>
        string.Concat(element.Descendants(
                Drawing + "t")
            .Select(item => item.Value)).Trim();

    private static InventoryExtractedRow VisualReviewRow() => new(
        1,
        "pptx:presentation;embedded-images",
        new SortedDictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["extractionblocker"] =
                NativeOfficeImageReader.RequiredBlocker,
        });

    private static string TableRowLocator(
        int slide,
        int table,
        int row) =>
        SlideLocator(slide) +
        ";table=" + table.ToString(
            CultureInfo.InvariantCulture) +
        ";row=" + row.ToString(
            CultureInfo.InvariantCulture);

    private static string TableCellLocator(
        int slide,
        int table,
        int row,
        int column) =>
        TableRowLocator(slide, table, row) +
        ";cell=" + column.ToString(
            CultureInfo.InvariantCulture);

    private static string ParagraphLocator(
        int slide,
        int shape,
        int paragraph,
        int character) =>
        SlideLocator(slide) +
        ";shape=" + shape.ToString(
            CultureInfo.InvariantCulture) +
        ";paragraph=" + paragraph.ToString(
            CultureInfo.InvariantCulture) +
        ";character=" + character.ToString(
            CultureInfo.InvariantCulture);

    private static string SlideLocator(int slide) =>
        "pptx:slide=" + slide.ToString(
            CultureInfo.InvariantCulture);

    private static bool IsPackageTitle(string value) =>
        value.Contains(
            "package", StringComparison.OrdinalIgnoreCase) ||
        value.Contains(
            "plan", StringComparison.OrdinalIgnoreCase);

    private static string Limit(string value, int maximum) =>
        value.Length <= maximum
            ? value
            : value[..maximum];
}
