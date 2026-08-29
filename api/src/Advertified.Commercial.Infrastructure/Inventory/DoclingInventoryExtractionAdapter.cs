using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
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
        var outputHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        return new InventoryExtractionResult(
            "docling", InventoryExtractionOptions.PinnedAdapterVersion,
            InventoryExtractionOptions.CurrentSchemaVersion, request.SourceHash,
            json, outputHash, rows);
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
            return [];
        }
        var rows = new List<InventoryExtractedRow>();
        var tableNumber = 0;
        foreach (var table in tables.EnumerateArray())
        {
            tableNumber++;
            rows.AddRange(ReadTable(table, tableNumber, rows.Count));
        }
        return rows;
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
            .ToDictionary(group => group.Key, group => NormalizeHeader(group.First().Text));
        var page = ReadPage(table);
        return values.Where(cell => cell.Row > headerRow)
            .GroupBy(cell => cell.Row)
            .OrderBy(group => group.Key)
            .Select((group, index) => new InventoryExtractedRow(
                rowOffset + index + 1,
                $"docling:page={page};table={tableNumber};row={group.Key + 1}",
                group.Where(cell => headers.ContainsKey(cell.Column) &&
                                    headers[cell.Column].Length > 0 && cell.Text.Length > 0)
                    .ToDictionary(cell => headers[cell.Column], cell => cell.Text)))
            .Where(row => row.Values.Count > 0).ToArray();
    }

    private static DoclingCell? ReadCell(JsonElement cell)
    {
        if (!cell.TryGetProperty("text", out var text) ||
            !cell.TryGetProperty("start_row_offset_idx", out var row) ||
            !cell.TryGetProperty("start_col_offset_idx", out var column))
        {
            return null;
        }
        return new DoclingCell(row.GetInt32(), column.GetInt32(), text.GetString()?.Trim() ?? "");
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

    private static string NormalizeHeader(string value) => new(
        value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private sealed record DoclingCell(int Row, int Column, string Text);
}
