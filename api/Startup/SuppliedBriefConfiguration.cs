using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Infrastructure.Brief;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Api.Startup;

internal static class SuppliedBriefConfiguration
{
    // This selects an implementation, not a model route or research permission.
    internal const string ModeKey = "SuppliedBrief:Mode";
    internal const string Disabled = "Disabled";
    internal const string Http = "Http";

    internal static void AddSuppliedBriefInterpretation(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ISuppliedBriefInterpretationStore, SuppliedBriefInterpretationStore>();
        var mode = builder.Configuration[ModeKey] ?? Disabled;
        if (mode == Disabled)
            builder.Services.AddScoped<ISuppliedBriefAgentClient, DisabledSuppliedBriefAgentClient>();
        else if (mode == Http)
        {
            var runtime = builder.Configuration.GetSection(AgentRuntimeOptions.SectionName)
                .Get<AgentRuntimeOptions>() ?? new AgentRuntimeOptions();
            if (!runtime.UsesHttp)
                throw new InvalidOperationException("HTTP brief understanding requires the HTTP runtime boundary.");
            builder.Services.AddHttpClient<HttpSuppliedBriefAgentClient>(AgentRuntimeClientConfiguration.Configure)
                .AddHttpMessageHandler<AiMonthlyBudgetHandler>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
            builder.Services.AddScoped<ISuppliedBriefAgentClient>(sp => sp.GetRequiredService<HttpSuppliedBriefAgentClient>());
        }
        else
            throw new InvalidOperationException("No approved supplied-brief implementation matches the configured mode.");
    }
}
