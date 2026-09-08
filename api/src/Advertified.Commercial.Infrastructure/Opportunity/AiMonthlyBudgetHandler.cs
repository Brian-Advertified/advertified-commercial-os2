using System.Net.Http.Json;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed class AiMonthlyBudgetHandler(
    AiMonthlyBudgetStore store) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var invocation = request.Content is null ? null :
            await request.Content.ReadFromJsonAsync<AgentInvocationRequest>(
                AgentRuntimeHttpSupport.WireJson, cancellationToken);
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
        var envelope = System.Text.Json.JsonSerializer.Deserialize<
            AiUsageEnvelope>(payload, AgentRuntimeHttpSupport.WireJson)
            ?? throw new InvalidOperationException(
                "The agent usage envelope is absent.");
        await store.CompleteAsync(
            reservation,
            envelope.Usage.IncrementalCostUsdMicros,
            CancellationToken.None);
        var replacement = new ByteArrayContent(payload);
        foreach (var header in response.Content.Headers)
            replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
        response.Content = replacement;
        return response;
    }

    private sealed record AiUsageEnvelope(AgentProviderUsage Usage);
}
