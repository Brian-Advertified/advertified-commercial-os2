using System.Net.Http.Headers;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class DoclingInventoryExtractionAdapter(
    HttpClient client,
    IOptions<InventoryExtractionOptions> options) : IDurableInventoryDocumentExtractionAdapter
{
    private const string SuccessStatus = "success";
    private const string PendingStatus = "pending";
    private const string StartedStatus = "started";
    private const string TaskNotFoundStatus = "task_not_found";
    private const int PollWaitSeconds = 30;
    private readonly InventoryExtractionOptions settings = options.Value;

    public string ProviderName => "docling";
    public string ProviderVersion => InventoryExtractionOptions.PinnedAdapterVersion;
    public bool SupportsIdempotentSubmission => false;
    public bool SupportsCancellation => false;

    public async Task<InventoryExtractionResult> ExtractAsync(
        InventoryExtractionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var submission = await SubmitAsync(request, string.Empty, cancellationToken);
            while (true)
            {
                var poll = await PollAsync(submission.ExternalTaskId, cancellationToken);
                if (poll.State == InventoryProviderTaskState.Completed) break;
                if (poll.State == InventoryProviderTaskState.Failed)
                    throw new InventoryExtractionUnavailableException();
            }
            return await ReadResultAsync(
                request, submission.ExternalTaskId, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InventoryExtractionUnavailableException();
        }
        catch (InventoryExtractionSubmissionRejectedException)
        {
            throw new InventoryExtractionUnavailableException();
        }
    }

    public async Task<InventoryExtractionSubmission> SubmitAsync(
        InventoryExtractionRequest request,
        string stableSubmissionKey,
        CancellationToken cancellationToken)
    {
        _ = stableSubmissionKey;
        using var form = CreateForm(request);
        using var response = await ReadJsonAsync(
            HttpMethod.Post, "/v1/convert/file/async", form, true, cancellationToken);
        var root = response.RootElement;
        if (!root.TryGetProperty("task_id", out var value) ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InventoryExtractionUnavailableException();
        }
        var status = ReadStatus(root, "task_status");
        return new InventoryExtractionSubmission(
            value.GetString()!, MapTaskState(status), status, root.GetRawText());
    }

    public async Task<InventoryExtractionPollResult> PollAsync(
        string externalTaskId,
        CancellationToken cancellationToken)
    {
        var path = $"/v1/status/poll/{Uri.EscapeDataString(externalTaskId)}?wait={PollWaitSeconds}";
        using var response = await ReadJsonAsync(
            HttpMethod.Get, path, null, false, cancellationToken);
        var root = response.RootElement;
        var status = ReadStatus(root, "task_status");
        return new InventoryExtractionPollResult(
            MapTaskState(status), status, ReadErrorCode(root), root.GetRawText());
    }

    public async Task<InventoryExtractionResult> ReadResultAsync(
        InventoryExtractionRequest request,
        string externalTaskId,
        CancellationToken cancellationToken)
    {
        using var result = await ReadJsonAsync(
            HttpMethod.Get, $"/v1/result/{Uri.EscapeDataString(externalTaskId)}",
            null, false, cancellationToken);
        return MapResult(request, result.RootElement);
    }

    public Task<bool> CancelAsync(
        string externalTaskId,
        CancellationToken cancellationToken) => Task.FromResult(false);

    private async Task<JsonDocument> ReadJsonAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        bool submission,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, path) { Content = content };
        message.Headers.Add("X-Api-Key", settings.ApiKey);
        using var response = await client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (!submission && response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return JsonDocument.Parse(
                    "{\"task_status\":\"task_not_found\"}");
            }
            if (submission)
            {
                throw new InventoryExtractionSubmissionRejectedException(
                    $"HTTP_{(int)response.StatusCode}");
            }
            throw new InventoryExtractionUnavailableException();
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static string ReadStatus(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var status) ||
            status.ValueKind != JsonValueKind.String)
            throw new InventoryExtractionUnavailableException();
        return status.GetString() ?? string.Empty;
    }

    private static InventoryProviderTaskState MapTaskState(string status) => status switch
    {
        PendingStatus => InventoryProviderTaskState.Pending,
        StartedStatus => InventoryProviderTaskState.Running,
        SuccessStatus => InventoryProviderTaskState.Completed,
        _ => InventoryProviderTaskState.Failed,
    };

    private static string? ReadErrorCode(JsonElement root)
    {
        if (ReadStatus(root, "task_status") == TaskNotFoundStatus)
        {
            return "DOCLING_TASK_NOT_FOUND";
        }
        if (root.TryGetProperty("failure", out var failure) &&
            failure.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            return "DOCLING_TASK_FAILURE";
        }
        return root.TryGetProperty("error_message", out var error) &&
               error.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(error.GetString())
            ? "DOCLING_TASK_ERROR"
            : null;
    }

    private static InventoryExtractionResult MapResult(
        InventoryExtractionRequest request,
        JsonElement root)
    {
        if (!root.TryGetProperty("status", out var status) ||
            status.GetString() != SuccessStatus ||
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
        form.Add(new StringContent("placeholder"), "image_export_mode");
        form.Add(new StringContent("false"), "include_images");
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
