using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.AspNetCore.Hosting;

namespace Advertified.Commercial.Api.Tests;

internal static class TestWebHostExtensions
{
    internal static IWebHostBuilder UseDeterministicInventoryProtection(
        this IWebHostBuilder builder)
    {
        builder.UseSetting("InventoryProtection:ObjectStoreMode", "InMemory");
        builder.UseSetting("InventoryProtection:ScannerMode", "Deterministic");
        builder.UseSetting("InventoryExtraction:Mode", "Deterministic");
        builder.UseSetting("InventoryProcessing:Paused", "false");
        return builder;
    }

    internal static IWebHostBuilder UseDeterministicAgentRuntime(
        this IWebHostBuilder builder)
    {
        builder.UseSetting("AgentRuntime:Mode", AgentRuntimeOptions.HttpDeterministicMode);
        builder.UseSetting("AgentRuntime:BaseUrl", "http://agent-runtime.test");
        builder.UseSetting("AgentRuntime:ServiceKey", "advertified-test-runtime-only");
        builder.UseSetting("AgentRuntime:Provider", AgentRuntimeOptions.DeterministicProvider);
        builder.UseSetting("AgentRuntime:DefaultModel", "fixture-v1");
        builder.UseSetting("AgentRuntime:DefaultCostCapMinor", "0");
        builder.UseSetting("AgentRuntime:CostCapsMinor:media_strategy", "0");
        builder.UseSetting("AgentRuntime:AllowLive", "false");
        builder.UseSetting("AgentRuntime:MaxAttempts", "1");
        return builder;
    }

    internal static IWebHostBuilder UseDeterministicTestDependencies(
        this IWebHostBuilder builder) =>
        builder.UseDeterministicInventoryProtection().UseDeterministicAgentRuntime();
}
