using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Infrastructure.Measurement;
using Advertified.Commercial.Infrastructure.Opportunity;
using Advertified.Commercial.Infrastructure.Planning;
using Advertified.Commercial.Infrastructure.Proposal;

namespace Advertified.Commercial.Api.Startup;

internal static class AgentRuntimeRegistration
{
    internal static void AddAgentRuntimeClients(this WebApplicationBuilder builder, AgentRuntimeOptions settings)
    {
        switch (settings.Mode)
        {
            case AgentRuntimeOptions.DisabledMode:
                builder.Services.AddScoped<IOpportunityAgentClient, DisabledAgentRuntimeClient>();
                builder.Services.AddScoped<IPlanningAgentClient, DisabledAgentRuntimeClient>();
                builder.Services.AddScoped<IProposalNarrativeClient, DisabledAgentRuntimeClient>();
                builder.Services.AddScoped<IMeasurementAgentClient, DisabledAgentRuntimeClient>();
                break;
            case AgentRuntimeOptions.HttpDeterministicMode:
                EnsureLocal(builder);
                AddHttpClients(builder);
                break;
            case AgentRuntimeOptions.HttpMode:
                if (settings.Provider == AgentRuntimeOptions.DeterministicProvider) EnsureLocal(builder);
                AddHttpClients(builder);
                break;
            default:
                throw new InvalidOperationException("The agent runtime mode is invalid.");
        }
    }

    private static void AddHttpClients(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IOpportunityAgentClient>(sp => sp.GetRequiredService<HttpOpportunityAgentClient>());
        builder.Services.AddScoped<IPlanningAgentClient>(sp => sp.GetRequiredService<HttpPlanningAgentClient>());
        builder.Services.AddScoped<IProposalNarrativeClient>(sp => sp.GetRequiredService<HttpProposalNarrativeClient>());
        builder.Services.AddScoped<IMeasurementAgentClient>(sp => sp.GetRequiredService<HttpMeasurementAgentClient>());
    }

    private static void EnsureLocal(WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Test"))
            throw new InvalidOperationException("The deterministic HTTP agent runtime is development/test only.");
    }
}
