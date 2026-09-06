using System.Net.Http.Json;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class PythonInventoryProjectionClient(
    HttpClient client,
    IOptions<AgentRuntimeOptions> options)
{
    internal const string SchemaVersion =
        "advertified.inventory-extraction.python.v1";
    internal const string ProjectorVersion =
        "advertified-docling-python/1.7.0";
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);
    private readonly AgentRuntimeOptions settings = options.Value;

    internal async Task<PythonInventoryProjection> ProjectAsync(
        string providerJson,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(providerJson);
            using var message = new HttpRequestMessage(
                HttpMethod.Post, "/v1/inventory-extraction/project")
            {
                Content = JsonContent.Create(new ProjectionRequest(
                    document.RootElement.Clone()), options: Json),
            };
            message.Headers.Add(
                "X-Advertified-Service-Key", settings.ServiceKey);
            using var response = await client.SendAsync(
                message, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InventoryExtractionUnavailableException();
            await using var stream = await response.Content.ReadAsStreamAsync(
                cancellationToken);
            var projection =
                await JsonSerializer.DeserializeAsync<PythonInventoryProjection>(
                    stream, Json, cancellationToken);
            if (projection is null ||
                projection.SchemaVersion != SchemaVersion ||
                projection.ProjectorVersion != ProjectorVersion ||
                projection.Rows is null ||
                projection.SourceElements is null ||
                projection.Warnings is null)
            {
                throw new InventoryExtractionUnavailableException();
            }
            return projection;
        }
        catch (JsonException)
        {
            throw new InventoryExtractionUnavailableException();
        }
        catch (HttpRequestException)
        {
            throw new InventoryExtractionUnavailableException();
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new InventoryExtractionUnavailableException();
        }
    }

    private sealed record ProjectionRequest(JsonElement ProviderDocument);
}

public sealed record PythonInventoryProjection(
    string SchemaVersion,
    string ProjectorVersion,
    IReadOnlyList<InventoryExtractedRow> Rows,
    IReadOnlyList<InventoryExtractedSourceElement> SourceElements,
    IReadOnlyList<string> Warnings);
