using Advertified.Commercial.Api.Startup;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class AgentRuntimeModeTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Disabled")]
    [InlineData("invalid")]
    [InlineData("InProcessDeterministic")]
    public void RuntimeRejectsRemovedOrUnsupportedModes(string mode)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Production" });

        Assert.Throws<InvalidOperationException>(() =>
            builder.AddAgentRuntimeClients(new AgentRuntimeOptions { Mode = mode }));
    }

    [Fact]
    public void ProductionRejectsDeterministicHttpFixtureRuntime()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Production" });

        Assert.Throws<InvalidOperationException>(() =>
            builder.AddAgentRuntimeClients(new AgentRuntimeOptions
            {
                Mode = AgentRuntimeOptions.HttpDeterministicMode,
                Provider = AgentRuntimeOptions.DeterministicProvider,
            }));
    }

    [Fact]
    public void ProductionAcceptsExplicitHttpRuntimeBoundary()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Production" });

        builder.AddAgentRuntimeClients(new AgentRuntimeOptions
        {
            Mode = AgentRuntimeOptions.HttpMode,
            Provider = AgentRuntimeOptions.BedrockProvider,
        });
    }
}
