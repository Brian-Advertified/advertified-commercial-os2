using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Infrastructure.Intelligence;
using Advertified.Commercial.Infrastructure.Measurement;
using Advertified.Commercial.Infrastructure.Opportunity;
using Advertified.Commercial.Infrastructure.Planning;
using Advertified.Commercial.Infrastructure.Proposal;

namespace Advertified.Commercial.Api.Startup;

internal static class AgentRuntimeRegistration
{
    internal static void AddAgentRuntimeClients(this WebApplicationBuilder builder, AgentRuntimeOptions settings)
    {
        if (!AgentRuntimeOptions.HasSupportedMode(settings))
            throw new InvalidOperationException("Advertified requires an explicit HTTP agent runtime mode.");
        if (!settings.UsesHttp)
            throw new InvalidOperationException("Advertified commercial intelligence requires the HTTP agent runtime boundary.");
        if (settings.Mode == AgentRuntimeOptions.HttpDeterministicMode ||
            settings.Provider == AgentRuntimeOptions.DeterministicProvider)
            EnsureLocal(builder);

        builder.Services.AddScoped<IOpportunityAgentClient>(sp => sp.GetRequiredService<HttpOpportunityAgentClient>());
        builder.Services.AddScoped<IMarketIntelligenceAgentClient>(sp => sp.GetRequiredService<HttpMarketIntelligenceAgentClient>());
        builder.Services.AddScoped<IAudienceIntelligenceAgentClient>(sp => sp.GetRequiredService<HttpAudienceIntelligenceAgentClient>());
        builder.Services.AddScoped<IMediaStrategyIntelligenceAgentClient>(sp => sp.GetRequiredService<HttpMediaStrategyIntelligenceAgentClient>());
        builder.Services.AddScoped<Advertified.Commercial.Application.LocationIntelligence.ILocationIntelligenceAgentClient>(sp => sp.GetRequiredService<Advertified.Commercial.Infrastructure.LocationIntelligence.HttpLocationIntelligenceAgentClient>());
        builder.Services.AddScoped<IInventoryIntelligenceAgentClient>(sp => sp.GetRequiredService<HttpInventoryIntelligenceAgentClient>());
        builder.Services.AddScoped<IProposalNarrativeClient>(sp => sp.GetRequiredService<HttpProposalNarrativeClient>());
        builder.Services.AddScoped<IMeasurementAgentClient>(sp => sp.GetRequiredService<HttpMeasurementAgentClient>());
    }

    private static void EnsureLocal(WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Test"))
            throw new InvalidOperationException("The deterministic HTTP agent runtime is development/test only.");
    }
}
