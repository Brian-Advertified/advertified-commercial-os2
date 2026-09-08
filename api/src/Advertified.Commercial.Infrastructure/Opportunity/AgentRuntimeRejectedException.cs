using System.Net;
using System.Text.Json;

namespace Advertified.Commercial.Infrastructure.Opportunity;

internal sealed class AgentRuntimeRejectedException : Exception
{
    private const string UnclassifiedStage = "UNCLASSIFIED";
    private static readonly string Accepted =
        nameof(Accepted).ToUpperInvariant();
    private static readonly string NotAccepted =
        "NOT_" + Accepted;
    private AgentRuntimeRejectedException(
        HttpStatusCode statusCode,
        string responseJson,
        string acceptance,
        string stage,
        string? provider,
        string? model,
        int? units,
        int? toolCalls,
        long? incrementalCostMinor,
        string? cacheStatus,
        string? providerRequestId,
        int? inputTokens,
        int? outputTokens,
        long? costUsdMicros) : base(
            "The agent runtime rejected the provider result.")
    {
        StatusCode = statusCode;
        ResponseJson = responseJson;
        Acceptance = acceptance;
        Stage = stage;
        Provider = provider;
        Model = model;
        Units = units;
        ToolCalls = toolCalls;
        IncrementalCostMinor = incrementalCostMinor;
        CacheStatus = cacheStatus;
        ProviderRequestId = providerRequestId;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CostUsdMicros = costUsdMicros;
    }

    internal HttpStatusCode StatusCode { get; }
    internal string ResponseJson { get; }
    internal string Acceptance { get; }
    internal string Stage { get; }
    internal string? Provider { get; }
    internal string? Model { get; }
    internal int? Units { get; }
    internal int? ToolCalls { get; }
    internal long? IncrementalCostMinor { get; }
    internal string? CacheStatus { get; }
    internal string? ProviderRequestId { get; }
    internal int? InputTokens { get; }
    internal int? OutputTokens { get; }
    internal long? CostUsdMicros { get; }
    internal bool HasDefinitiveProviderAcceptance =>
        string.Equals(Acceptance, Accepted, StringComparison.Ordinal) ||
        string.Equals(Acceptance, NotAccepted, StringComparison.Ordinal);
    internal bool HasBillableAcceptedUsage =>
        string.Equals(Acceptance, Accepted, StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(Provider) &&
        !string.IsNullOrWhiteSpace(Model) &&
        Units is > 0 &&
        ToolCalls is >= 0 &&
        IncrementalCostMinor is >= 0 &&
        !string.IsNullOrWhiteSpace(CacheStatus) &&
        !string.IsNullOrWhiteSpace(ProviderRequestId) &&
        InputTokens is >= 0 &&
        OutputTokens is > 0 &&
        CostUsdMicros is > 0;

    internal static AgentRuntimeRejectedException Read(
        HttpStatusCode statusCode,
        string responseJson)
    {
        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var detail = document.RootElement.GetProperty("detail");
            var usage = detail.GetProperty("usage");
            return new(
                statusCode,
                responseJson,
                detail.GetProperty("provider_acceptance").GetString() ?? string.Empty,
                detail.GetProperty("stage").GetString() ?? UnclassifiedStage,
                ReadString(usage, "provider"),
                ReadString(usage, "model"),
                ReadInt(usage, "units"),
                ReadInt(usage, "tool_calls"),
                ReadLong(usage, "incremental_cost_minor"),
                ReadString(usage, "cache_status"),
                ReadString(usage, "provider_request_id"),
                ReadInt(usage, "input_tokens"),
                ReadInt(usage, "output_tokens"),
                ReadLong(usage, "incremental_cost_usd_micros"));
        }
        catch (Exception error) when (
            error is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return new(
                statusCode, responseJson, string.Empty, "HTTP_RESPONSE",
                null, null, null, null, null, null, null, null, null, null);
        }
    }

    private static string? ReadString(JsonElement usage, string name) =>
        usage.ValueKind == JsonValueKind.Object &&
        usage.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static int? ReadInt(JsonElement usage, string name) =>
        usage.ValueKind == JsonValueKind.Object &&
        usage.TryGetProperty(name, out var value) &&
        value.TryGetInt32(out var result)
            ? result : null;

    private static long? ReadLong(JsonElement usage, string name) =>
        usage.ValueKind == JsonValueKind.Object &&
        usage.TryGetProperty(name, out var value) &&
        value.TryGetInt64(out var result)
            ? result : null;
}
