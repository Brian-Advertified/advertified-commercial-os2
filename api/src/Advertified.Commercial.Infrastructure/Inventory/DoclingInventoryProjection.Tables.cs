using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class DoclingInventoryProjection
{
    private static InventoryExtractedRow[] ReadTable(
        JsonElement table,
        int tableNumber,
        int rowOffset,
        IReadOnlyList<TextItem> texts)
    {
        var cells = ReadCells(table);
        if (cells.Length == 0) return [];
        var page = ReadPage(table);
        var sourceRows = cells
            .GroupBy(cell => cell.Row)
            .Select(group => new InventoryTableRow(
                group.Key,
                group.GroupBy(cell => cell.Column)
                    .ToDictionary(
                        item => item.Key,
                        item => item.First().Text)))
            .ToArray();
        var keyValue = InventoryKeyValueTableProjection.Project(
            sourceRows,
            rowOffset,
            row => TableLocator(page, tableNumber, row),
            (row, column) => CellLocator(
                page, tableNumber, row, column));
        if (keyValue.Length > 0)
            return keyValue;
        var radioSchedule = ReadRadioSchedule(
            sourceRows,
            texts,
            tableNumber,
            page,
            rowOffset);
        if (radioSchedule.Length > 0)
            return radioSchedule;
        var headerRow = SelectHeaderRow(cells);
        if (headerRow is null)
        {
            return InventoryHeaderlessRateTableProjection.Project(
                sourceRows,
                rowOffset,
                row => TableLocator(page, tableNumber, row),
                (row, column) => CellLocator(
                    page, tableNumber, row, column));
        }
        var dataRows = sourceRows
            .Where(row => row.SourceRow > headerRow.Value)
            .ToArray();
        var headers = RepairOcrHeaders(
            Headers(cells, headerRow.Value),
            cells,
            headerRow.Value,
            dataRows);
        var projectedDataRows = FillDownContextColumns(
            headers,
            dataRows);
        var schedule = ReadSchedule(
            cells,
            headers,
            projectedDataRows,
            tableNumber,
            page,
            headerRow.Value,
            rowOffset);
        if (schedule.Length > 0) return schedule;

        var confidence = MinimumConfidence(cells);
        var method = confidence.HasValue
            ? MasterDataCodes.InventoryExtractionMethods.Ocr
            : MasterDataCodes.InventoryExtractionMethods.Tabular;
        var projected = InventoryTabularProjection.Project(
            headers,
            projectedDataRows,
            rowOffset,
            row => TableLocator(page, tableNumber, row),
            (row, column) => CellLocator(
                page, tableNumber, row, column),
            (row, column) => CellConfidence(
                cells, row, column));
        return ApplyTableContext(headers, projected)
            .Select(row => row with
            {
                ExtractionMethod = method,
                Confidence = confidence,
            })
            .ToArray();
    }

    private static InventoryExtractedRow[] ReadSchedule(
        DoclingCell[] cells,
        IReadOnlyDictionary<int, string> headers,
        IReadOnlyList<InventoryTableRow> dataRows,
        int tableNumber,
        int page,
        int headerRow,
        int rowOffset)
    {
        var dates = headers
            .Select(item => (
                item.Key,
                Match: DatePattern().Match(item.Value)))
            .Where(item => item.Match.Success)
            .ToDictionary(
                item => item.Key,
                item => item.Match.Groups["date"].Value);
        if (dates.Count < 2) return [];

        var confidence = MinimumConfidence(cells);
        var method = confidence.HasValue
            ? MasterDataCodes.InventoryExtractionMethods.Ocr
            : MasterDataCodes.InventoryExtractionMethods.Tabular;
        var result = new List<InventoryExtractedRow>();
        foreach (var row in dataRows)
        {
            var time = row.Cells
                .OrderBy(item => item.Key)
                .FirstOrDefault(item =>
                    !dates.ContainsKey(item.Key));
            foreach (var date in dates)
            {
                if (!row.Cells.TryGetValue(
                        date.Key, out var raw) ||
                    !TryMoney(
                        raw, out var money, out var currency))
                {
                    continue;
                }
                var name = SellableName(raw, money);
                if (name.Length == 0) continue;

                var locator = CellLocator(
                    page, tableNumber, row.SourceRow, date.Key);
                var values = new SortedDictionary<string, string>(
                    StringComparer.Ordinal)
                {
                    ["currency"] = currency,
                    ["name"] = name,
                    ["rate"] = money,
                    ["ratetype"] =
                        MasterDataCodes.RateTypes.SpotRate,
                    ["scheduledate"] = date.Value,
                };
                if (!string.IsNullOrWhiteSpace(time.Value)) values["timeslot"] = time.Value.Trim();

                var dataConfidence = CellConfidence(
                    cells, row.SourceRow, date.Key);
                var fieldConfidences = values.Keys.ToDictionary(
                    key => key,
                    key => key switch
                    {
                        "scheduledate" => CellConfidence(
                            cells, headerRow, date.Key),
                        "timeslot" => CellConfidence(
                            cells, row.SourceRow, time.Key),
                        _ => dataConfidence,
                    });
                result.Add(InventoryScheduleEvidence.Create(
                    rowOffset + result.Count + 1, values, locator,
                    CellLocator(page, tableNumber, headerRow, date.Key),
                    CellLocator(page, tableNumber, row.SourceRow, time.Key),
                    method, confidence, fieldConfidences));
            }
        }
        return result.ToArray();
    }

    private static DoclingCell[] ReadCells(
        JsonElement table)
    {
        if (!table.TryGetProperty("data", out var data) ||
            !data.TryGetProperty(
                "table_cells", out var cells) ||
            cells.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        return cells.EnumerateArray()
            .Select(ReadCell)
            .Where(cell => cell is not null)
            .Select(cell => cell!)
            .ToArray();
    }

    private static DoclingCell? ReadCell(
        JsonElement cell)
    {
        if (!cell.TryGetProperty("text", out var text) ||
            !cell.TryGetProperty(
                "start_row_offset_idx", out var row) ||
            !cell.TryGetProperty(
                "start_col_offset_idx", out var column))
        {
            return null;
        }
        return new DoclingCell(
            row.GetInt32(),
            column.GetInt32(),
            text.GetString()?.Trim() ?? string.Empty,
            ReadConfidence(cell));
    }

    private static int? SelectHeaderRow(
        IReadOnlyList<DoclingCell> cells)
    {
        var selected = cells.Select(cell => cell.Row)
            .Distinct()
            .Order()
            .Take(6)
            .Select(row => new
            {
                Row = row,
                Score = cells
                    .Where(cell => cell.Row == row)
                    .Count(cell =>
                        InventoryCandidateNormalizer
                            .RecognizesHeader(
                                InventoryTabularProjection
                                    .NormalizeHeader(cell.Text)) ||
                        DatePattern().IsMatch(cell.Text)),
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Row)
            .First();
        return selected.Score > 0
            ? selected.Row
            : null;
    }

    private static Dictionary<int, string> Headers(
        IReadOnlyList<DoclingCell> cells,
        int headerRow) =>
        cells.Where(cell => cell.Row <= headerRow)
            .GroupBy(cell => cell.Column)
            .Select(group => new
            {
                Column = group.Key,
                Text = group
                    .OrderByDescending(cell => cell.Row)
                    .Select(cell => cell.Text)
                    .FirstOrDefault(text =>
                        !string.IsNullOrWhiteSpace(text))
                    ?? string.Empty,
            })
            .Where(item => item.Text.Length > 0)
            .ToDictionary(
                item => item.Column,
                item => item.Text);

    private static decimal? MinimumConfidence(
        IEnumerable<DoclingCell> cells)
    {
        var values = cells
            .Where(cell => cell.Confidence.HasValue)
            .Select(cell => cell.Confidence!.Value)
            .ToArray();
        return values.Length == 0
            ? null
            : values.Min();
    }

    private static decimal? CellConfidence(
        IEnumerable<DoclingCell> cells,
        int row,
        int column) =>
        cells.FirstOrDefault(cell =>
            cell.Row == row &&
            cell.Column == column)?.Confidence;
}
