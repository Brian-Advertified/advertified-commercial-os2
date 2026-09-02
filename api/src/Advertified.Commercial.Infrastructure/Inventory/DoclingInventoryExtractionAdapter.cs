using System.Net.Http.Headers;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class DoclingInventoryExtractionAdapter(
    HttpClient client,
    IOptions<InventoryExtractionOptions> options) : IInventoryDocumentExtractionAdapter
{
    private readonly InventoryExtractionOptions settings = options.Value;

    public async Task<InventoryExtractionResult> ExtractAsync(
        InventoryExtractionRequest request,
        CancellationToken cancellationToken)
    {
        using var form = CreateForm(request);
        using var message = new HttpRequestMessage(HttpMethod.Post, "/v1/convert/file")
        {
            Content = form,
        };
        message.Headers.Add("X-Api-Key", settings.ApiKey);
        using var response = await client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InventoryExtractionUnavailableException();
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var body = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = body.RootElement;
        if (!root.TryGetProperty("status", out var status) ||
            status.GetString() != "success" ||
            !root.TryGetProperty("document", out var document) ||
            !document.TryGetProperty("json_content", out var structured))
        {
            throw new InventoryExtractionUnavailableException();
        }
        var json = structured.ValueKind == JsonValueKind.String
            ? structured.GetString() ?? "{}" : structured.GetRawText();
        var rows = ReadRows(json);
        return InventoryExtractionContract.Create(
            "docling", InventoryExtractionOptions.PinnedAdapterVersion,
            InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash, json, rows);
    }

    private static MultipartFormDataContent CreateForm(InventoryExtractionRequest request)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(request.Content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(request.MediaType);
        form.Add(file, "files", request.FileName);
        form.Add(new StringContent("json"), "to_formats");
        form.Add(new StringContent("text"), "to_formats");
        form.Add(new StringContent("embedded"), "image_export_mode");
        form.Add(new StringContent("true"), "do_ocr");
        form.Add(new StringContent("true"), "do_table_structure");
        form.Add(new StringContent("true"), "abort_on_error");
        return form;
    }

    private static List<InventoryExtractedRow> ReadRows(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("tables", out var tables) ||
            tables.ValueKind != JsonValueKind.Array)
        {
            return ReadKeyValueRows(document.RootElement);
        }
        var rows = new List<InventoryExtractedRow>();
        var tableNumber = 0;
        foreach (var table in tables.EnumerateArray())
        {
            tableNumber++;
            rows.AddRange(ReadTable(table, tableNumber, rows.Count));
        }
        var documentValues = ReadKeyValueRows(document.RootElement).SingleOrDefault();
        return rows.Count == 0 || documentValues is null
            ? rows.Count > 0 ? rows : ReadKeyValueRows(document.RootElement)
            : rows.Select(row => MergeDocumentValues(row, documentValues)).ToList();
    }

    private static InventoryExtractedRow MergeDocumentValues(
        InventoryExtractedRow row,
        InventoryExtractedRow documentValues)
    {
        var values = new SortedDictionary<string, string>(
            row.Values.ToDictionary(item => item.Key, item => item.Value),
            StringComparer.Ordinal);
        foreach (var item in documentValues.Values) values.TryAdd(item.Key, item.Value);
        var locators = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in documentValues.FieldLocators ??
                new Dictionary<string, string>())
            locators[item.Key] = item.Value;
        foreach (var item in row.FieldLocators ?? new Dictionary<string, string>())
            locators[item.Key] = item.Value;
        var confidences = new Dictionary<string, decimal?>(StringComparer.Ordinal);
        foreach (var item in documentValues.FieldConfidences ??
                new Dictionary<string, decimal?>())
            confidences[item.Key] = item.Value;
        foreach (var item in row.FieldConfidences ?? new Dictionary<string, decimal?>())
            confidences[item.Key] = item.Value;
        return row with
        {
            Values = values,
            FieldLocators = locators,
            FieldConfidences = confidences,
        };
    }

    private static InventoryExtractedRow[] ReadTable(
        JsonElement table,
        int tableNumber,
        int rowOffset)
    {
        if (!table.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("table_cells", out var cells))
        {
            return [];
        }
        var values = cells.EnumerateArray()
            .Select(ReadCell)
            .Where(cell => cell is not null)
            .Select(cell => cell!).ToArray();
        if (values.Length == 0)
        {
            return [];
        }
        var headerRow = values.Min(cell => cell.Row);
        var headers = values.Where(cell => cell.Row == headerRow)
            .GroupBy(cell => cell.Column)
            .ToDictionary(group => group.Key, group => group.First().Text);
        var rows = values.Where(cell => cell.Row > headerRow)
            .GroupBy(cell => cell.Row)
            .Select(group => new InventoryTableRow(
                group.Key,
                group.GroupBy(cell => cell.Column)
                    .ToDictionary(item => item.Key, item => item.First().Text)));
        var page = ReadPage(table);
        var confidence = values.Where(cell => cell.Confidence.HasValue)
            .Select(cell => cell.Confidence!.Value).DefaultIfEmpty().Min();
        decimal? measuredConfidence = values.Any(cell => cell.Confidence.HasValue)
            ? confidence : null;
        var method = measuredConfidence.HasValue
            ? MasterDataCodes.InventoryExtractionMethods.Ocr
            : MasterDataCodes.InventoryExtractionMethods.Tabular;
        return InventoryTabularProjection.Project(
            headers, rows, rowOffset,
            row => $"docling:page={page};table={tableNumber};row={row + 1}",
            (row, column) =>
                $"docling:page={page};table={tableNumber};row={row + 1};cell={column + 1}",
            (row, column) => values.FirstOrDefault(cell =>
                cell.Row == row && cell.Column == column)?.Confidence)
            .Select(row => row with
            {
                ExtractionMethod = method,
                Confidence = measuredConfidence,
            }).ToArray();
    }

    private static DoclingCell? ReadCell(JsonElement cell)
    {
        if (!cell.TryGetProperty("text", out var text) ||
            !cell.TryGetProperty("start_row_offset_idx", out var row) ||
            !cell.TryGetProperty("start_col_offset_idx", out var column))
        {
            return null;
        }
        return new DoclingCell(
            row.GetInt32(), column.GetInt32(), text.GetString()?.Trim() ?? "",
            ReadConfidence(cell));
    }

    private static List<InventoryExtractedRow> ReadKeyValueRows(JsonElement root)
    {
        if (!root.TryGetProperty("texts", out var texts) ||
            texts.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var pages = new SortedSet<int>();
        var locators = new Dictionary<string, string>(StringComparer.Ordinal);
        var confidences = new Dictionary<string, decimal?>(StringComparer.Ordinal);
        decimal? confidence = null;
        var textNumber = 0;
        foreach (var item in texts.EnumerateArray())
        {
            textNumber++;
            if (!item.TryGetProperty("text", out var textValue)) continue;
            var text = textValue.GetString()?.Trim();
            var page = ReadPage(item);
            var itemConfidence = ReadConfidence(item);
            var segmentNumber = 0;
            foreach (var segment in (text ?? string.Empty).Split(
                [';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries))
            {
                segmentNumber++;
                var separator = segment.IndexOf(':');
                if (separator <= 0 || separator == segment.Length - 1) continue;
                var key = InventoryTabularProjection.NormalizeHeader(segment[..separator]);
                if (key.Length == 0 || !values.TryAdd(
                        key, segment[(separator + 1)..].Trim())) continue;
                pages.Add(page);
                locators[key] =
                    $"docling:page={page};text={textNumber};segment={segmentNumber}";
                confidences[key] = itemConfidence;
                if (itemConfidence.HasValue)
                {
                    confidence = confidence.HasValue
                        ? Math.Min(confidence.Value, itemConfidence.Value)
                        : itemConfidence;
                }
            }
        }
        if (values.Count == 0) return [];
        var locator = "docling:pages=" + string.Join(',', pages.DefaultIfEmpty(1));
        return [new InventoryExtractedRow(
            1, locator, values,
            confidence.HasValue
                ? MasterDataCodes.InventoryExtractionMethods.Ocr
                : MasterDataCodes.InventoryExtractionMethods.KeyValue,
            confidence, locators, confidences)];
    }

    private static decimal? ReadConfidence(JsonElement value)
    {
        foreach (var name in new[] { "confidence", "ocr_confidence" })
        {
            if (value.TryGetProperty(name, out var confidence) &&
                confidence.TryGetDecimal(out var result) && result is >= 0 and <= 1)
            {
                return result;
            }
        }
        return null;
    }

    private static int ReadPage(JsonElement table)
    {
        if (table.TryGetProperty("prov", out var provenance) &&
            provenance.ValueKind == JsonValueKind.Array &&
            provenance.GetArrayLength() > 0 &&
            provenance[0].TryGetProperty("page_no", out var page))
        {
            return page.GetInt32();
        }
        return 1;
    }

    private sealed record DoclingCell(
        int Row,
        int Column,
        string Text,
        decimal? Confidence);
}
