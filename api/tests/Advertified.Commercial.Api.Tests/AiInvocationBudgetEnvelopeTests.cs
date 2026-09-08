using System.Net.Http.Json;
using System.Text.Json;
using Advertified.Commercial.Infrastructure.Opportunity;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class AiInvocationBudgetEnvelopeTests
{
    [Theory]
    [InlineData("deterministic", false)]
    [InlineData("bedrock", true)]
    public async Task ReadsNestedInvocationWithoutDeserializingAgentSpecificInput(
        string provider, bool live)
    {
        var run = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://runtime.test/")
        {
            Content = JsonContent.Create(new
            {
                invocation = new
                {
                    run_id = run,
                    provider_policy = new
                    {
                        provider, model = "fixture", temperature = 0,
                        timeout_seconds = 30, max_attempts = 1,
                        cost_cap_minor = 5, allow_live = live,
                    },
                },
                planning = new { objective = "Deterministic fixture" },
            }),
        };
        var invocation = await AiMonthlyBudgetHandler.ReadInvocationAsync(
            request, CancellationToken.None);
        Assert.Equal(run, invocation.RunId);
        Assert.Equal(provider, invocation.ProviderPolicy.Provider);
        Assert.Equal(live, invocation.ProviderPolicy.AllowLive);
    }

    [Fact]
    public async Task MissingInvocationCannotBypassBudgetBoundary()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://runtime.test/");
        await Assert.ThrowsAsync<JsonException>(() =>
            AiMonthlyBudgetHandler.ReadInvocationAsync(request, CancellationToken.None));
    }

    [Theory]
    [InlineData("bedrock", "fixture", 1000, 1, true)]
    [InlineData("deterministic", "fixture", 1000, 1, false)]
    [InlineData("bedrock", "different", 1000, 1, false)]
    [InlineData("bedrock", "fixture", 0, 0, false)]
    [InlineData("bedrock", "fixture", 50001, 5, false)]
    public void LiveUsageMustMatchReservedProviderModelAndCost(string provider, string model,
        long micros, long minor, bool accepted)
    {
        var policy = new AgentProviderPolicy("bedrock", "fixture", 0, 30, 1, 5, true);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            usage = new { provider, model, units = 1, tool_calls = 0, incremental_cost_minor = minor,
                cache_status = "LIVE", provider_request_id = "fixture:request", incremental_cost_usd_micros = micros },
        });
        if (accepted) Assert.Equal(micros, AiMonthlyBudgetHandler.ReadUsage(payload, policy).IncrementalCostUsdMicros);
        else Assert.Throws<InvalidOperationException>(() => AiMonthlyBudgetHandler.ReadUsage(payload, policy));
    }
}
