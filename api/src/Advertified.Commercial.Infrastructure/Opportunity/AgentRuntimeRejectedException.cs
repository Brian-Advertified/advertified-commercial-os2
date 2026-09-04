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
        ProviderRequestId = providerRequestId;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CostUsdMicros = costUsdMicros;
    }

    internal HttpStatusCode StatusCode { get; }
    internal string ResponseJson { get; }
    internal string Acceptance { get; }
    internal string Stage { get; }
    internal string? ProviderRequestId { get; }
    internal int? InputTokens { get; }
    internal int? OutputTokens { get; }
    internal long? CostUsdMicros { get; }
    internal bool HasDefinitiveProviderAcceptance =>
        string.Equals(Acceptance, Accepted, StringComparison.Ordinal) ||
        string.Equals(Acceptance, NotAccepted, StringComparison.Ordinal);

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
                usage.ValueKind == JsonValueKind.Object
                    ? usage.GetProperty("provider_request_id").GetString()
                    : null,
                ReadInt(usage, "input_tokens"),
                ReadInt(usage, "output_tokens"),
                ReadLong(usage, "incremental_cost_usd_micros"));
        }
        catch (Exception error) when (
            error is JsonException or InvalidOperationException)
        {
            return new(
                statusCode, responseJson, string.Empty, "HTTP_RESPONSE",
                null, null, null, null);
        }
    }

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
