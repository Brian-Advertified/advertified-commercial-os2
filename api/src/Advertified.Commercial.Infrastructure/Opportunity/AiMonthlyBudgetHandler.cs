using System.Net.Http.Json;
using System.Text.Json;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed class AiMonthlyBudgetHandler(
    AiMonthlyBudgetStore store) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var invocation = await ReadInvocationAsync(request, cancellationToken);
        if (invocation?.ProviderPolicy is not
            { Provider: AgentRuntimeOptions.BedrockProvider, AllowLive: true })
            return await base.SendAsync(request, cancellationToken);

        var reservation = await store.ReserveAsync(
            invocation, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsByteArrayAsync(
            cancellationToken);

        var usage = response.IsSuccessStatusCode
            ? ReadUsage(payload, invocation.ProviderPolicy)
            : ReadAcceptedRejectedUsage(payload, invocation.ProviderPolicy);
        if (usage is not null)
        {
            await store.CompleteAsync(
                reservation,
                usage.IncrementalCostUsdMicros,
                CancellationToken.None);
        }

        ReplaceContent(response, payload);
        return response;
    }

    internal static AgentProviderUsage ReadUsage(byte[] payload, AgentProviderPolicy policy)
    {
        using var document = JsonDocument.Parse(payload);
        var usage = document.RootElement.GetProperty("usage")
            .Deserialize<AgentProviderUsage>(AgentRuntimeHttpSupport.WireJson)
            ?? throw new InvalidOperationException("The agent usage envelope is absent.");
        ValidateUsage(usage, policy);
        return usage;
    }

    internal static AgentProviderUsage? ReadAcceptedRejectedUsage(
        byte[] payload,
        AgentProviderPolicy policy)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var detail = document.RootElement.GetProperty("detail");
            if (!string.Equals(
                    detail.GetProperty("provider_acceptance").GetString(),
                    "ACCEPTED",
                    StringComparison.Ordinal))
            {
                return null;
            }

            if (!detail.TryGetProperty("usage", out var usageElement) ||
                usageElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            var usage = usageElement.Deserialize<AgentProviderUsage>(
                AgentRuntimeHttpSupport.WireJson);
            if (usage is null) return null;
            ValidateUsage(usage, policy);
            return usage;
        }
        catch (Exception error) when (
            error is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            // A rejected response without an exact accepted-usage receipt stays fully reserved.
            return null;
        }
    }

    private static void ValidateUsage(
        AgentProviderUsage usage,
        AgentProviderPolicy policy)
    {
        if (usage.Provider != policy.Provider || usage.Model != policy.Model ||
            usage.IncrementalCostUsdMicros <= 0 || usage.IncrementalCostMinor <= 0 ||
            usage.IncrementalCostUsdMicros > checked(policy.CostCapMinor * 10_000L) ||
            usage.IncrementalCostMinor > policy.CostCapMinor || string.IsNullOrWhiteSpace(usage.ProviderRequestId))
        {
            throw new InvalidOperationException(
                "The live agent usage does not match its reservation.");
        }
    }

    private static void ReplaceContent(
        HttpResponseMessage response,
        byte[] payload)
    {
        var replacement = new ByteArrayContent(payload);
        foreach (var header in response.Content.Headers)
            replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
        response.Content.Dispose();
        response.Content = replacement;
    }

    internal static async Task<AgentInvocationRequest> ReadInvocationAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is null)
            throw new JsonException("The agent invocation is absent.");
        using var document = await request.Content.ReadFromJsonAsync<JsonDocument>(
            cancellationToken: cancellationToken)
            ?? throw new JsonException("The agent invocation is absent.");
        return document.RootElement.GetProperty("invocation")
            .Deserialize<AgentInvocationRequest>(AgentRuntimeHttpSupport.WireJson)
            ?? throw new JsonException("The agent invocation is absent.");
    }
}
