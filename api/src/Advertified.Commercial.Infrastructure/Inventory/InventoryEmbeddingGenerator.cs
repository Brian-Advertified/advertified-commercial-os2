using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Inventory;

public interface IInventoryEmbeddingGenerator
{
    long MaximumCostUsdMicros { get; }

    Task<InventoryEmbeddingGeneration> GenerateAsync(
        string canonicalText,
        CancellationToken cancellationToken);
}

public sealed record InventoryEmbeddingGeneration(
    string Provider,
    string Model,
    string Region,
    IReadOnlyList<float> Embedding,
    string ProviderRequestId,
    int InputTokens,
    long IncrementalCostUsdMicros);

public sealed class DisabledInventoryEmbeddingGenerator : IInventoryEmbeddingGenerator
{
    public long MaximumCostUsdMicros => 0;

    public Task<InventoryEmbeddingGeneration> GenerateAsync(
        string canonicalText,
        CancellationToken cancellationToken) => throw new InvalidOperationException(
            "Inventory embedding generation is disabled.");
}

public sealed class DeterministicInventoryEmbeddingGenerator : IInventoryEmbeddingGenerator
{
    public long MaximumCostUsdMicros => 0;

    public Task<InventoryEmbeddingGeneration> GenerateAsync(
        string canonicalText,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var vector = new float[InventoryEmbeddingOptions.Dimensions];
        foreach (var token in canonicalText.Split(
            [' ', '\n', '\r', '\t', '|', ',', '.', ':', ';', '/', '-'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token.ToLowerInvariant()));
            var index = (int)(BitConverter.ToUInt32(hash, 0) % (uint)vector.Length);
            vector[index] += (hash[4] & 1) == 0 ? 1f : -1f;
        }
        Normalize(vector);
        return Task.FromResult(new InventoryEmbeddingGeneration(
            "deterministic", "fixture-inventory-embedding-v1", "local",
            vector, $"fixture-{Guid.NewGuid():N}",
            canonicalText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, 0));
    }

    private static void Normalize(float[] vector)
    {
        var magnitude = Math.Sqrt(vector.Sum(value => value * value));
        if (magnitude == 0) vector[0] = 1f;
        else
        {
            for (var index = 0; index < vector.Length; index++)
            {
                vector[index] = (float)(vector[index] / magnitude);
            }
        }
    }
}

public sealed class HttpInventoryEmbeddingGenerator(
    HttpClient client,
    IOptions<InventoryEmbeddingOptions> options) : IInventoryEmbeddingGenerator
{
    private readonly InventoryEmbeddingOptions settings = options.Value;
    public long MaximumCostUsdMicros => settings.MaximumRequestCostUsdMicros;

    public async Task<InventoryEmbeddingGeneration> GenerateAsync(
        string canonicalText,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            "v1/inventory-embeddings",
            new InventoryEmbeddingRequest(
                canonicalText, settings.Model, settings.OutputDimensions,
                settings.Normalize), cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<InventoryEmbeddingResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The embedding provider response is empty.");
        if (result.Embedding.Count != InventoryEmbeddingOptions.Dimensions ||
            result.Model != InventoryEmbeddingOptions.TitanModel ||
            result.Region != InventoryEmbeddingOptions.BedrockRegion ||
            result.IncrementalCostUsdMicros < 0)
        {
            throw new InvalidOperationException("The embedding provider response is invalid.");
        }
        return new(
            "bedrock", result.Model, result.Region, result.Embedding,
            result.ProviderRequestId, result.InputTokens,
            result.IncrementalCostUsdMicros);
    }

    private sealed record InventoryEmbeddingRequest(
        [property: JsonPropertyName("canonical_text")] string CanonicalText,
        string Model,
        int Dimensions,
        bool Normalize);

    private sealed record InventoryEmbeddingResponse(
        string Model,
        string Region,
        IReadOnlyList<float> Embedding,
        [property: JsonPropertyName("provider_request_id")] string ProviderRequestId,
        [property: JsonPropertyName("input_tokens")] int InputTokens,
        [property: JsonPropertyName("incremental_cost_usd_micros")]
        long IncrementalCostUsdMicros);
}
