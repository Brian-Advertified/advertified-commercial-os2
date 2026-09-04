using System.Net.Http.Headers;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class DoclingInventoryExtractionAdapter(
    HttpClient client,
    IOptions<InventoryExtractionOptions> options) :
    IDurableInventoryDocumentExtractionAdapter
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
            var submission = await SubmitAsync(
                request, string.Empty, cancellationToken);
            while (true)
            {
                var poll = await PollAsync(
                    submission.ExternalTaskId, cancellationToken);
                if (poll.State == InventoryProviderTaskState.Completed) break;
                if (poll.State == InventoryProviderTaskState.Failed)
                    throw new InventoryExtractionUnavailableException();
            }
            return await ReadResultAsync(
                request, submission.ExternalTaskId, cancellationToken);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
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
            HttpMethod.Post, "/v1/convert/file/async", form, true,
            cancellationToken);
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
        var path = "/v1/status/poll/" +
            Uri.EscapeDataString(externalTaskId) +
            "?wait=" + PollWaitSeconds;
        using var response = await ReadJsonAsync(
            HttpMethod.Get, path, null, false, cancellationToken);
        var root = response.RootElement;
        var status = ReadStatus(root, "task_status");
        return new InventoryExtractionPollResult(
            MapTaskState(status), status, ReadErrorCode(root),
            root.GetRawText());
    }

    public async Task<InventoryExtractionResult> ReadResultAsync(
        InventoryExtractionRequest request,
        string externalTaskId,
        CancellationToken cancellationToken)
    {
        using var result = await ReadJsonAsync(
            HttpMethod.Get,
            "/v1/result/" + Uri.EscapeDataString(externalTaskId),
            null, false, cancellationToken);
        var mapped = MapResult(request, result.RootElement);
        return await EnrichEmbeddedOfficeImagesAsync(
            request, mapped, cancellationToken);
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
        using var message = new HttpRequestMessage(method, path)
        {
            Content = content,
        };
        message.Headers.Add("X-Api-Key", settings.ApiKey);
        using var response = await client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (!submission &&
                response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return JsonDocument.Parse(
                    "{\"task_status\":\"task_not_found\"}");
            }
            if (submission)
            {
                throw new InventoryExtractionSubmissionRejectedException(
                    "HTTP_" + (int)response.StatusCode);
            }
            throw new InventoryExtractionUnavailableException();
        }
        await using var stream = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        return await JsonDocument.ParseAsync(
            stream, cancellationToken: cancellationToken);
    }

    private static string ReadStatus(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var status) ||
            status.ValueKind != JsonValueKind.String)
        {
            throw new InventoryExtractionUnavailableException();
        }
        return status.GetString() ?? string.Empty;
    }

    private static InventoryProviderTaskState MapTaskState(
        string status) => status switch
    {
        PendingStatus => InventoryProviderTaskState.Pending,
        StartedStatus => InventoryProviderTaskState.Running,
        SuccessStatus => InventoryProviderTaskState.Completed,
        _ => InventoryProviderTaskState.Failed,
    };

    private static string? ReadErrorCode(JsonElement root)
    {
        if (ReadStatus(root, "task_status") == TaskNotFoundStatus)
            return "DOCLING_TASK_NOT_FOUND";
        if (root.TryGetProperty("failure", out var failure) &&
            failure.ValueKind is not (
                JsonValueKind.Null or JsonValueKind.Undefined))
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
            ? structured.GetString() ?? "{}"
            : structured.GetRawText();
        var rows = DoclingInventoryProjection.ReadRows(request, json);
        var provider = InventoryExtractionContract.Create(
            "docling",
            InventoryExtractionOptions.PinnedAdapterVersion,
            InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash,
            json,
            rows);
        var projected = NativeOfficeInventoryProjection.Apply(
            request, provider);
        return InventorySourceContextProjection.Apply(
            request, projected);
    }

    private static MultipartFormDataContent CreateForm(
        InventoryExtractionRequest request)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(request.Content);
        file.Headers.ContentType =
            MediaTypeHeaderValue.Parse(request.MediaType);
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
}
