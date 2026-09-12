using System.Text;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class ProductionModelConfigurationTests
{
    [Fact]
    public async Task ProductionJsonBindsEveryRequiredModelRouteWithoutEnvironmentKeyAmbiguity()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "appsettings.Production.example.json");
        var json = (await File.ReadAllTextAsync(path)).Replace(
            "REPLACE_WITH_APPROVED_MODEL", "synthetic-config-test-model", StringComparison.Ordinal);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var configuration = new ConfigurationBuilder().AddJsonStream(stream)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AgentRuntime:Mode"] = AgentRuntimeOptions.HttpMode,
                ["AgentRuntime:Provider"] = AgentRuntimeOptions.BedrockProvider,
                ["AgentRuntime:AllowLive"] = "true",
                ["AgentRuntime:DefaultCostCapMinor"] = "5",
            }).Build();
        var options = configuration.GetSection(AgentRuntimeOptions.SectionName).Get<AgentRuntimeOptions>()!;
        Assert.True(AgentRuntimeOptions.HasSafeProviderPolicy(options));
        Assert.True(AgentRuntimeOptions.HasSafeRoutes(options));
        Assert.Equal("synthetic-config-test-model", options.ModelFor(
            MasterDataCodes.AgentTypes.BriefDrafting, "SUPPLIED_BRIEF_UNDERSTANDING"));
        Assert.Equal("synthetic-config-test-model", options.ModelFor(
            MasterDataCodes.AgentTypes.InventoryIntelligence, "SOURCE_TRANSCRIPTION"));
        Assert.False(options.Models.ContainsKey("audience"));
        Assert.False(options.Models.ContainsKey("media_planning"));

        // Every route in this template must be required by the canonical runtime contract.
        // Removing one cannot silently fall back to a default or another model.
        foreach (var route in options.Models.Keys.ToArray())
        {
            var value = options.Models[route];
            options.Models.Remove(route);
            Assert.False(AgentRuntimeOptions.HasSafeProviderPolicy(options));
            options.Models.Add(route, value);
        }
    }
}
