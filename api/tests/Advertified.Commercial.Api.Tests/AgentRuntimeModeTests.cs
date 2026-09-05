using Advertified.Commercial.Api.Startup;
using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class AgentRuntimeModeTests
{
    [Fact]
    public async Task ProductionDisabledResolvesUnavailableClientsAndCreatesNoFixture()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.AddAgentRuntimeClients(new AgentRuntimeOptions());
        using var provider = builder.Services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var planning = Assert.IsType<DisabledAgentRuntimeClient>(services.GetRequiredService<IPlanningAgentClient>());
        var opportunity = Assert.IsType<DisabledAgentRuntimeClient>(services.GetRequiredService<IOpportunityAgentClient>());
        var narrative = Assert.IsType<DisabledAgentRuntimeClient>(services.GetRequiredService<IProposalNarrativeClient>());
        var measurement = Assert.IsType<DisabledAgentRuntimeClient>(services.GetRequiredService<IMeasurementAgentClient>());
        Func<Task>[] requests = [
            () => planning.ProposeAudiencesAsync(null!, default),
            () => planning.ProposeMediaMixAsync(null!, default),
            () => planning.InterpretInventoryAsync(null!, default),
            () => opportunity.InvokeAsync(null!, default),
            () => narrative.CreateAsync(null!, default),
            () => measurement.InterpretAsync(null!, default),
        ];
        foreach (var request in requests)
            await Assert.ThrowsAsync<AgentRuntimeUnavailableException>(request);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData(AgentRuntimeOptions.InProcessMode)]
    [InlineData(AgentRuntimeOptions.HttpDeterministicMode)]
    [InlineData(AgentRuntimeOptions.HttpMode)]
    public void ProductionRejectsInvalidOrFixtureRuntime(string mode)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        Assert.Throws<InvalidOperationException>(() => builder.AddAgentRuntimeClients(new AgentRuntimeOptions { Mode = mode }));
    }
}
