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
        if (!response.IsSuccessStatusCode)
            return response;
        var payload = await response.Content.ReadAsByteArrayAsync(
            cancellationToken);
        var usage = ReadUsage(payload, invocation.ProviderPolicy);
        await store.CompleteAsync(
            reservation,
            usage.IncrementalCostUsdMicros,
            CancellationToken.None);
        var replacement = new ByteArrayContent(payload);
        foreach (var header in response.Content.Headers)
            replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
        response.Content.Dispose();
        response.Content = replacement;
        return response;
    }

    internal static AgentProviderUsage ReadUsage(byte[] payload, AgentProviderPolicy policy)
    {
        using var document = JsonDocument.Parse(payload);
        var usage = document.RootElement.GetProperty("usage")
            .Deserialize<AgentProviderUsage>(AgentRuntimeHttpSupport.WireJson)
            ?? throw new InvalidOperationException("The agent usage envelope is absent.");
        if (usage.Provider != policy.Provider || usage.Model != policy.Model ||
            usage.IncrementalCostUsdMicros <= 0 || usage.IncrementalCostMinor <= 0 ||
            usage.IncrementalCostUsdMicros > checked(policy.CostCapMinor * 10_000L) ||
            usage.IncrementalCostMinor > policy.CostCapMinor || string.IsNullOrWhiteSpace(usage.ProviderRequestId))
            throw new InvalidOperationException("The live agent usage does not match its reservation.");
        return usage;
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
